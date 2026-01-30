using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json;

[assembly: CommandClass(typeof(SvfDwgUnifiedBoundary.Commands))]

namespace SvfDwgUnifiedBoundary
{
    public class Commands
    {
        [CommandMethod("PROCESS_BOUNDARY_FROM_EXTERNALID")]
        public void ProcessBoundaryFromExternalId()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            if (!db.TileMode)
            {
                ed.WriteMessage("\n❌ Switch to MODEL space.");
                return;
            }

            string jsonPath =
                @"D:\Buniyad Byte\POC 2\svf-dwg-dbId-boundary\server\storage\clicks.json";

            if (!File.Exists(jsonPath))
            {
                ed.WriteMessage("\n❌ clicks.json not found.");
                return;
            }

            List<ClickData> clicks =
                JsonConvert.DeserializeObject<List<ClickData>>(
                    File.ReadAllText(jsonPath));

            if (clicks == null || clicks.Count == 0)
            {
                ed.WriteMessage("\n❌ No click data found.");
                return;
            }

            ClickData last = clicks[clicks.Count - 1];

            if (string.IsNullOrWhiteSpace(last.externalId))
            {
                ed.WriteMessage("\n❌ externalId missing.");
                return;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                long handleValue = Convert.ToInt64(last.externalId, 16);
                ObjectId objId =
                    db.GetObjectId(false, new Handle(handleValue), 0);

                Entity ent =
                    tr.GetObject(objId, OpenMode.ForRead) as Entity;

                if (ent == null)
                {
                    ed.WriteMessage("\n❌ Entity not found.");
                    return;
                }

                ed.WriteMessage("\n✔ Entity type: " + ent.GetType().Name);

                List<Curve> boundaryCurves =
                    ExtractBoundaryCurves(ent, tr);

                if (boundaryCurves.Count == 0)
                {
                    ed.WriteMessage("\n❌ No boundary curves extracted.");
                    return;
                }

                Extents3d ext = boundaryCurves[0].GeometricExtents;
                foreach (Curve c in boundaryCurves)
                    ext.AddExtents(c.GeometricExtents);

                BlockTableRecord btr =
                    (BlockTableRecord)tr.GetObject(
                        db.CurrentSpaceId, OpenMode.ForWrite);

                const double spacing = 100.0;
                int barCount = 0;
                double totalLength = 0;

                for (double y = ext.MinPoint.Y;
                     y <= ext.MaxPoint.Y;
                     y += spacing)
                {
                    Line scanLine =
                        new Line(
                            new Point3d(ext.MinPoint.X - 1000, y, 0),
                            new Point3d(ext.MaxPoint.X + 1000, y, 0));

                    List<Point3d> intersections =
                        new List<Point3d>();

                    foreach (Curve bc in boundaryCurves)
                    {
                        Point3dCollection pts =
                            new Point3dCollection();

                        scanLine.IntersectWith(
                            bc,
                            Intersect.OnBothOperands,
                            pts,
                            IntPtr.Zero,
                            IntPtr.Zero);

                        foreach (Point3d p in pts)
                            intersections.Add(p);
                    }

                    intersections.Sort((a, b) => a.X.CompareTo(b.X));

                    for (int i = 0; i + 1 < intersections.Count; i += 2)
                    {
                        Line bar =
                            new Line(
                                intersections[i],
                                intersections[i + 1]);

                        totalLength += bar.Length;
                        barCount++;

                        btr.AppendEntity(bar);
                        tr.AddNewlyCreatedDBObject(bar, true);
                    }
                }

                ed.WriteMessage("\n===============================");
                ed.WriteMessage("\nBars created : " + barCount);
                ed.WriteMessage("\nTotal length : " + totalLength.ToString("F2"));
                ed.WriteMessage("\n===============================");

                tr.Commit();
            }
        }

        // =====================================================
        // FIXED BOUNDARY EXTRACTION
        // =====================================================
        private static List<Curve> ExtractBoundaryCurves(
            Entity ent,
            Transaction tr)
        {
            List<Curve> curves = new List<Curve>();

            // ================= HATCH =================
            Hatch hatch = ent as Hatch;
            if (hatch != null)
            {
                Plane plane =
                    new Plane(
                        new Point3d(0, 0, hatch.Elevation),
                        hatch.Normal);

                for (int i = 0; i < hatch.NumberOfLoops; i++)
                {
                    HatchLoop loop = hatch.GetLoopAt(i);

                    foreach (Curve2d c2d in loop.Curves)
                    {
                        if (c2d is LineSegment2d ls)
                        {
                            Point3d p1 =
                                plane.EvaluatePoint(ls.StartPoint);
                            Point3d p2 =
                                plane.EvaluatePoint(ls.EndPoint);

                            curves.Add(new Line(p1, p2));
                        }
                        else if (c2d is CircularArc2d arc2d)
                        {
                            Point3d center =
                                plane.EvaluatePoint(arc2d.Center);

                            Arc arc =
                                new Arc(
                                    center,
                                    hatch.Normal,
                                    arc2d.Radius,
                                    arc2d.StartAngle,
                                    arc2d.EndAngle);

                            curves.Add(arc);
                        }
                    }
                }

                return curves;
            }

            // ================= POLYLINE =================
            if (ent is Polyline pl)
            {
                curves.Add(pl);
                return curves;
            }

            // ================= OTHERS =================
            DBObjectCollection exploded = new DBObjectCollection();
            ent.Explode(exploded);

            foreach (DBObject obj in exploded)
            {
                if (obj is Curve c)
                    curves.Add(c);
            }

            return curves;
        }
    }

    // =====================================================
    // JSON MODEL
    // =====================================================
    public class ClickData
    {
        public string externalId { get; set; }
        public int dbId { get; set; }
        public long timestamp { get; set; }
    }
}

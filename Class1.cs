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

            string clickPath =
                @"D:\Buniyad Byte\POC 2\svf-dwg-dbId-boundary\server\storage\clicks.json";

            string resultPath =
                @"D:\Buniyad Byte\POC 2\svf-dwg-dbId-boundary\server\storage\clicks_result.json";

            if (!File.Exists(clickPath))
            {
                ed.WriteMessage("\n❌ clicks.json not found.");
                return;
            }

            List<ClickData> clicks =
                JsonConvert.DeserializeObject<List<ClickData>>(
                    File.ReadAllText(clickPath));

            if (clicks == null || clicks.Count == 0)
            {
                ed.WriteMessage("\n❌ No click data found.");
                return;
            }

            ClickData click = clicks[0];

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                long handleValue = Convert.ToInt64(click.externalId, 16);
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

                List<Curve> boundary =
                    ExtractBoundaryCurves(ent, tr);

                if (boundary.Count == 0)
                {
                    ed.WriteMessage("\n❌ No boundary found.");
                    return;
                }

                Extents3d ext = boundary[0].GeometricExtents;
                foreach (Curve c in boundary)
                    ext.AddExtents(c.GeometricExtents);

                BlockTableRecord btr =
                    (BlockTableRecord)tr.GetObject(
                        db.CurrentSpaceId, OpenMode.ForWrite);

                const double spacing = 100.0;

                int barIndex = 1;
                double totalLength = 0;

                List<BarResult> bars =
                    new List<BarResult>();

                ed.WriteMessage("\n---------------- BAR LENGTHS ----------------");

                for (double y = ext.MinPoint.Y;
                     y <= ext.MaxPoint.Y;
                     y += spacing)
                {
                    Line scanLine =
                        new Line(
                            new Point3d(ext.MinPoint.X - 1000, y, 0),
                            new Point3d(ext.MaxPoint.X + 1000, y, 0));

                    List<Point3d> pts =
                        new List<Point3d>();

                    foreach (Curve bc in boundary)
                    {
                        Point3dCollection hits =
                            new Point3dCollection();

                        scanLine.IntersectWith(
                            bc,
                            Intersect.OnBothOperands,
                            hits,
                            IntPtr.Zero,
                            IntPtr.Zero);

                        foreach (Point3d p in hits)
                            pts.Add(p);
                    }

                    pts.Sort((a, b) => a.X.CompareTo(b.X));

                    for (int i = 0; i + 1 < pts.Count; i += 2)
                    {
                        Line bar =
                            new Line(pts[i], pts[i + 1]);

                        double len = bar.Length;

                        totalLength += len;

                        bars.Add(
                            new BarResult
                            {
                                index = barIndex,
                                length = Math.Round(len, 2)
                            });

                        ed.WriteMessage(
                            $"\nBar {barIndex} length : {len:F2}");

                        btr.AppendEntity(bar);
                        tr.AddNewlyCreatedDBObject(bar, true);

                        barIndex++;
                    }
                }

                ed.WriteMessage("\n=================================");
                ed.WriteMessage($"\nTotal Bars   : {barIndex - 1}");
                ed.WriteMessage($"\nTotal Length : {totalLength:F2}");
                ed.WriteMessage("\n=================================");

                // ================= SAVE RESULT JSON =================
                ResultJson result =
                    new ResultJson
                    {
                        externalId = click.externalId,
                        bars = bars,
                        totalBars = barIndex - 1,
                        totalLength = Math.Round(totalLength, 2),
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                File.WriteAllText(
                    resultPath,
                    JsonConvert.SerializeObject(result, Formatting.Indented));

                ed.WriteMessage(
                    $"\n✔ Result JSON saved: clicks_result.json");

                tr.Commit();
            }
        }

        // ================= BOUNDARY EXTRACTION =================
        private static List<Curve> ExtractBoundaryCurves(
            Entity ent,
            Transaction tr)
        {
            List<Curve> curves = new List<Curve>();

            if (ent is Hatch hatch)
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
                            curves.Add(
                                new Line(
                                    plane.EvaluatePoint(ls.StartPoint),
                                    plane.EvaluatePoint(ls.EndPoint)));
                        }
                        else if (c2d is CircularArc2d arc)
                        {
                            curves.Add(
                                new Arc(
                                    plane.EvaluatePoint(arc.Center),
                                    hatch.Normal,
                                    arc.Radius,
                                    arc.StartAngle,
                                    arc.EndAngle));
                        }
                    }
                }
                return curves;
            }

            if (ent is Polyline pl)
            {
                curves.Add(pl);
                return curves;
            }

            DBObjectCollection exploded =
                new DBObjectCollection();
            ent.Explode(exploded);

            foreach (DBObject obj in exploded)
            {
                if (obj is Curve c)
                    curves.Add(c);
            }

            return curves;
        }
    }

    // ================= JSON MODELS =================
    public class ClickData
    {
        public string externalId { get; set; }
        public int dbId { get; set; }
        public long timestamp { get; set; }
    }

    public class BarResult
    {
        public int index { get; set; }
        public double length { get; set; }
    }

    public class ResultJson
    {
        public string externalId { get; set; }
        public List<BarResult> bars { get; set; }
        public int totalBars { get; set; }
        public double totalLength { get; set; }
        public long timestamp { get; set; }
    }
}

const urn = "dXJuOmFkc2sub2JqZWN0czpvcy5vYmplY3Q6YnVuaXlhZGJ5dGUtcmViYXItcG9jLTAwMS9iZWFtX3JlYmFyLmR3Zw"; // 🔴 replace this

const options = {
  env: "AutodeskProduction",
  getAccessToken: async (callback) => {
    const res = await fetch("/api/token");
    const data = await res.json();
    callback(data.access_token, data.expires_in);
  }
};

Autodesk.Viewing.Initializer(options, () => {
  const viewer = new Autodesk.Viewing.GuiViewer3D(
    document.getElementById("viewer")
  );

  viewer.start();

  Autodesk.Viewing.Document.load(
    "urn:" + urn,
    (doc) => {
      const defaultModel = doc.getRoot().getDefaultGeometry();
      viewer.loadDocumentNode(doc, defaultModel);
    },
    (err) => console.error(err)
  );

  // 🔹 Capture externalId (AutoCAD Handle)
  viewer.addEventListener(
    Autodesk.Viewing.SELECTION_CHANGED_EVENT,
    (e) => {
      if (!e.dbIdArray || e.dbIdArray.length === 0) return;

      const dbId = e.dbIdArray[0];

      viewer.getProperties(dbId, async (props) => {
        const externalId = props.externalId; // ✅ HANDLE

        if (!externalId) {
          alert("No externalId found");
          return;
        }

        await fetch("/api/click", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            externalId,
            dbId,
            timestamp: Date.now()
          })
        });

        alert("Captured handle: " + externalId);
      });
    }
  );
});

import express from "express";
import fetch from "node-fetch";
import dotenv from "dotenv";
import fs from "fs";
import path from "path";

dotenv.config();

const app = express();
app.use(express.json());
app.use(express.static("../client"));

const STORAGE_DIR = path.join(process.cwd(), "storage");
const STORAGE_FILE = path.join(STORAGE_DIR, "clicks.json");

// ensure storage folder exists
if (!fs.existsSync(STORAGE_DIR)) {
  fs.mkdirSync(STORAGE_DIR, { recursive: true });
}

// ================= TOKEN =================
app.get("/api/token", async (req, res) => {
  const params = new URLSearchParams();
  params.append("grant_type", "client_credentials");
  params.append("scope", "viewables:read");

  const response = await fetch(
    "https://developer.api.autodesk.com/authentication/v2/token",
    {
      method: "POST",
      headers: {
        Authorization:
          "Basic " +
          Buffer.from(
            process.env.APS_CLIENT_ID +
              ":" +
              process.env.APS_CLIENT_SECRET
          ).toString("base64"),
        "Content-Type": "application/x-www-form-urlencoded"
      },
      body: params
    }
  );

  const token = await response.json();
  res.json(token);
});

// ================= CLICK =================
app.post("/api/click", (req, res) => {
  const click = {
    externalId: req.body.externalId || null,
    dbId: req.body.dbId || null,
    timestamp: Date.now()
  };

  // 🔥 OVERWRITE FILE (NOT APPEND)
  fs.writeFileSync(
    STORAGE_FILE,
    JSON.stringify([click], null, 2)
  );

  // 🖥️ TERMINAL LOG (VERY CLEAR)
  console.log("====================================");
  console.log("📍 NEW VIEWER CLICK RECEIVED");
  console.log("externalId :", click.externalId);
  console.log("dbId       :", click.dbId);
  console.log("timestamp  :", click.timestamp);
  console.log("====================================");

  res.json({ ok: true });
});

app.listen(3000, () => {
  console.log("🚀 Backend running at http://localhost:3000");
});

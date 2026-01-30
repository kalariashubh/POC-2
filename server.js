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
const STORAGE = path.join(STORAGE_DIR, "clicks.json");

// ✅ Ensure storage folder & file
if (!fs.existsSync(STORAGE_DIR)) {
  fs.mkdirSync(STORAGE_DIR);
}

if (!fs.existsSync(STORAGE)) {
  fs.writeFileSync(STORAGE, "[]", "utf8");
}

// ================= APS TOKEN =================
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

// ================= SAVE CLICK =================
app.post("/api/click", (req, res) => {
  const { externalId, dbId } = req.body;

  if (!externalId) {
    return res.status(400).json({ error: "externalId missing" });
  }

  let data = [];
  try {
    const raw = fs.readFileSync(STORAGE, "utf8").trim();
    data = raw ? JSON.parse(raw) : [];
  } catch {
    data = [];
  }

  data.push({
    externalId,
    dbId,
    timestamp: Date.now()
  });

  fs.writeFileSync(STORAGE, JSON.stringify(data, null, 2));
  res.json({ ok: true });
});

// ================= START SERVER =================
app.listen(3000, () =>
  console.log("🚀 http://localhost:3000")
);

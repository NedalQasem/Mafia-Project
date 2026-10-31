// Tiny static server for the HTML mockups (and the sprite exporter page): node Tools/ui-gen/serve.js [port]
// Serves Docs/mockups on http://localhost:8765 and accepts POST /save?name=... to write PNGs/JSON into Assets/_Project/Art/UI/Match.
const http = require("http"), fs = require("fs"), path = require("path");
const root = path.resolve(__dirname, "../../Docs/mockups");
const out = path.resolve(__dirname, "../../Assets/_Project/Art/UI/Match");
const port = Number(process.argv[2]) || 8765;
const types = { ".html": "text/html; charset=utf-8", ".png": "image/png", ".js": "text/javascript", ".json": "application/json" };
http.createServer((q, r) => {
  const u = new URL(q.url, "http://x");
  if (q.method === "POST" && u.pathname === "/save") {
    const name = path.basename(u.searchParams.get("name") || "x.png");
    const chunks = [];
    q.on("data", c => chunks.push(c));
    q.on("end", () => {
      const dp = u.searchParams.get("dir"); const dir = dp === "shots" ? path.resolve(__dirname, "../../Assets/Screenshots") : dp === "family" ? path.resolve(__dirname, "../../Assets/_Project/Art/UI/Family") : dp === "reward" ? path.resolve(__dirname, "../../Assets/_Project/Art/UI/Reward") : dp === "notice" ? path.resolve(__dirname, "../../Assets/_Project/Art/UI/QuickNotice") : dp === "nav" ? path.resolve(__dirname, "../../Assets/_Project/Art/UI/Nav") : dp === "bag" ? path.resolve(__dirname, "../../Assets/_Project/Art/UI/Bag") : out;   // shots = comparison images, not sprites
      fs.mkdirSync(dir, { recursive: true });
      const body = Buffer.concat(chunks).toString();
      const data = body.startsWith("data:") ? Buffer.from(body.split(",")[1], "base64") : Buffer.from(body);
      fs.writeFileSync(path.join(dir, name), data);
      r.writeHead(200, { "Access-Control-Allow-Origin": "*" }); r.end("ok " + data.length);
    });
    return;
  }
  // /art/... serves the exported sprites (for contact sheets)
  const f = u.pathname.startsWith("/art/") ? path.join(out, decodeURIComponent(u.pathname.slice(5))) : path.join(root, decodeURIComponent(u.pathname));
  fs.readFile(f, (e, d) => {
    if (e) { r.writeHead(404); r.end("no"); return; }
    r.writeHead(200, { "Content-Type": types[path.extname(f)] || "application/octet-stream" }); r.end(d);
  });
}).listen(port, () => console.log("up " + port));

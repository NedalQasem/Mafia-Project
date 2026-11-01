using System;

namespace MafiaNight.RoomBot
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string productId = Env("EOS_PRODUCT_ID"), sandboxId = Env("EOS_SANDBOX_ID"), deploymentId = Env("EOS_DEPLOYMENT_ID"), clientId = Env("EOS_CLIENT_ID"), clientSecret = Env("EOS_CLIENT_SECRET");
            string api = Env("API_URL") ?? "https://api.mena-gaming.com", botKey = Env("BOT_KEY");
            if (productId == null || sandboxId == null || deploymentId == null || clientId == null || clientSecret == null || botKey == null)
            { Log.Error("EOS_PRODUCT_ID, EOS_SANDBOX_ID, EOS_DEPLOYMENT_ID, EOS_CLIENT_ID, EOS_CLIENT_SECRET and BOT_KEY are needed (API_URL is optional)."); return 2; }

            var eos = new Eos();
            if (!eos.Start(productId, sandboxId, deploymentId, clientId, clientSecret)) return 3;
            if (!eos.Login("Mafia Office")) { Log.Error("EOS login did not finish"); return 4; }

            var keeper = new Keeper(eos, api, botKey);
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; Log.Info("Stopping..."); keeper.Stop(); };
            keeper.Run();
            eos.Shutdown();
            return keeper.Failed ? 5 : 0;
        }

        private static string Env(string name) { var v = Environment.GetEnvironmentVariable(name); return string.IsNullOrWhiteSpace(v) ? null : v.Trim(); }
    }
}

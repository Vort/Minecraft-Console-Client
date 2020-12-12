using System;

namespace MinecraftClient.ChatBots
{
    public class BoatFlyer : ChatBot
    {
        int tps;
        int tickCount;

        int loadedChunks;
        int loadedChunksPrev;
        int vehicleTeleports;

        double boatSpeed;
        double boatMaxSpeed; // Blocks per second

        double wanderingRange;

        Mapping.Location boatLoc;
        Mapping.Location boatDestLoc;
        Mapping.Location boatMoveVector;

        Random rnd;

        public override void Initialize()
        {
            tps = 20;
            tickCount = 0;

            loadedChunks = 0;
            loadedChunksPrev = 0;
            vehicleTeleports = 0;

            boatSpeed = 0.0;
            boatMaxSpeed = 25.0;

            wanderingRange = 100000.0;

            rnd = new Random();

            LogToConsole("BoatFlyer is started");
            GenerateDestLoc();
        }

        void GenerateDestLoc()
        {
            boatDestLoc.X = (rnd.NextDouble() * 2.0 - 1.0) * wanderingRange;
            boatDestLoc.Z = (rnd.NextDouble() * 2.0 - 1.0) * wanderingRange;
            LogToConsole($"New destination selected: x: {boatDestLoc.X:0}, z: {boatDestLoc.Z:0}");
        }

        void UpdateMoveVector()
        {
            double dx = boatDestLoc.X - boatLoc.X;
            double dz = boatDestLoc.Z - boatLoc.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            boatMoveVector = new Mapping.Location(dx / distance, 0.0, dz / distance);
        }

        public override void GetText(string text)
        {
            if (text == "§c§cRegister using /register <your password> ")
                SendText("/register 12345678");
            if (text == "§c§cYou have reached the maximum number of users allowed to register from the same IP")
                DisconnectAndExit();
            if (text == "§c§cPlease login using /login <your password>" ||
                text == "§6§6Register success, please /login")
                SendText("/login 12345678");
            if (text.StartsWith("§") && text.Contains("/survival"))
                SendText("/survival");
        }

        public override void OnTeleport(int teleportID)
        {
            var newLoc = GetCurrentLocation();
            bool isSrcZero = boatLoc.X == 0.0 && boatLoc.Z == 0.0;
            bool isDstZero = newLoc.X == 0.0 && newLoc.Z == 0.0;
            LogToConsole($"Teleport #{teleportID}: x: {newLoc.X:0.0}, y: {newLoc.Y:0.00}, z: {newLoc.Z:0.0}");

            if (!isSrcZero && !isDstZero && boatLoc.Distance(newLoc) > 1000.0)
            {
                LogToConsole($"Suspicious teleportation. Exiting...");
                DisconnectAndExit();
            }

            boatLoc = newLoc;
            boatLoc.Y += 0.45; // Boat height adjust
            UpdateMoveVector();
        }

        public override void OnVehicleTeleport(double x, double y, double z, float yaw, float pitch)
        {
            double distance = Math.Sqrt(
                Math.Pow(x - boatLoc.X, 2) +
                Math.Pow(y - boatLoc.Y, 2) +
                Math.Pow(z - boatLoc.Z, 2));
            LogToConsole($"Vehicle teleport: distance: {distance:0.0}");
            boatLoc.X = x;
            boatLoc.Y = y;
            boatLoc.Z = z;
            boatSpeed = 0.0;
            vehicleTeleports++;
            UpdateMoveVector();
        }

        public override void OnDeath()
        {
            LogToConsole($"Dead. It is better to exit.");
            DisconnectAndExit();
        }

        public override void OnChunkLoaded(int chunkX, int chunkZ)
        {
            loadedChunks++;
        }

        void Tick()
        {
            if (boatLoc.X == 0.0 && boatLoc.Z == 0.0)
                return;

            double dx = boatDestLoc.X - boatLoc.X;
            double dz = boatDestLoc.Z - boatLoc.Z;
            double distToDest = Math.Sqrt(dx * dx + dz * dz);
            if (distToDest < 50.0)
            {
                GenerateDestLoc();
                UpdateMoveVector();
            }

            tickCount++;
            if (tickCount % (tps * 2) == 0)
                LogToConsole($"Boat loc: x: {boatLoc.X:0}, y: {boatLoc.Y:0.00}, z: {boatLoc.Z:0}");
            if (tickCount % (tps * 60) == 0)
            {
                LogToConsole($"Uptime: {tickCount / (tps * 60)} minutes");
                LogToConsole($"Vehicle teleports: {vehicleTeleports}");
                LogToConsole($"Chunks loaded: {loadedChunks} (+{loadedChunks - loadedChunksPrev})");
                LogToConsole($"Distance to destination: {distToDest:0}");
                loadedChunksPrev = loadedChunks;
            }

            if (boatLoc.Y > 256.0)
            {
                // 10 seconds until full speed
                boatSpeed += boatMaxSpeed / (tps * 10);
                if (boatSpeed > boatMaxSpeed)
                    boatSpeed = boatMaxSpeed;
                boatLoc += boatMoveVector * boatSpeed / tps;
            }
            VehicleMove(boatLoc);

            if (tickCount % 2 == 0)
            {
                if (boatLoc.Y < 300.0)
                    boatLoc.Y += 0.35;
                else
                    boatLoc.Y += 0.25;
            }
            else
                boatLoc.Y -= 0.25;
        }

        public override void Update()
        {
            for (int i = 0; i < 2; i++)
                Tick();
        }
    }
}
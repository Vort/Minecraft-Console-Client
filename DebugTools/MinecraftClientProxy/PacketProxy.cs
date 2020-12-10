using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace MinecraftClient.Protocol.Handlers
{
    class PacketProxy
    {
        private int compression_treshold = 0;
        private bool handshake_phase = true;
        private bool login_phase = true;
        TcpClient client;
        TcpClient server;
        StreamWriter sw;

        public PacketProxy(TcpClient client, TcpClient server)
        {
            this.client = client;
            this.server = server;
            sw = File.AppendText("output.log");
        }

        public void Run()
        {
            Thread t = new Thread(() =>
            {
                try
                {
                    do { Thread.Sleep(100); }
                    while (Update(true));
                }
                catch (System.IO.IOException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            });
            t.Name = "UpdaterServer";
            t.Start();

            t = new Thread(() =>
            {
                try
                {
                    do { Thread.Sleep(100); }
                    while (Update(false));
                }
                catch (System.IO.IOException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            });
            t.Name = "UpdaterClient";
            t.Start();
        }

        private bool Update(bool server)
        {
            TcpClient c = server ? this.server : this.client;
            if (c.Client == null || !c.Connected) { return false; }
            try
            {
                while (c.Client.Available > 0)
                {
                    int packetID = 0;
                    byte[] packetData = new byte[] { };
                    byte[] packetRawData = new byte[] { };
                    readNextPacket(c, ref packetID, ref packetData, ref packetRawData);
                    handlePacket(packetID, (byte[])packetData.Clone(), server);
                    (server ? this.client : this.server).Client.Send(packetRawData);
                }
            }
            catch (SocketException) { return false; }
            return true;
        }

        private void readNextPacket(TcpClient c, ref int packetID, ref byte[] packetData, ref byte[] packetRawData)
        {
            int size = readNextVarIntRAW(c);
            packetData = readDataRAW(c, size);
            packetRawData = concatBytes(getVarInt(size), packetData);

            if (compression_treshold > 0)
            {
                int size_uncompressed = readNextVarInt(ref packetData);
                if (size_uncompressed != 0)
                    packetData = ZlibUtils.Decompress(packetData, size_uncompressed);
            }

            packetID = readNextVarInt(ref packetData);
        }

        private void Log(string message)
        {
            Console.Write(message);
            sw.Write(message);
        }
        private void LogLine(string message)
        {
            Log(message);
            Console.WriteLine();
            sw.WriteLine();
            sw.Flush();
        }

        private void handlePacket(int packetID, byte[] packetData, bool server)
        {
            //Console.WriteLine((server ? "[S -> C] 0x" : "[C -> S] 0x") + packetID.ToString("x2"));
            if (login_phase)
            {
                if (server)
                {
                    switch (packetID)
                    {
                        case 0x00:
                            LogLine("[S -> C] Login rejected");
                            break;
                        case 0x01:
                            LogLine("[S -> C] Encryption request");
                            LogLine(@"[WARNING] ENCRYPTION IS NOT SUPPORTED BY PROXY !!");
                            break;
                        case 0x02:
                            login_phase = false;
                            LogLine("[S -> C] Login successfull");
                            break;
                        case 0x03:
                            compression_treshold = readNextVarInt(ref packetData);
                            LogLine("[S -> C] Compression Treshold: " + compression_treshold);
                            break;
                    }
                }
                else
                {
                    switch (packetID)
                    {
                        case 0x00:
                            LogLine("[C -> S] " + (handshake_phase ? "Handshake" : "Login request"));
                            handshake_phase = false;
                            break;
                    }
                }
            }
            else
            {
                if (!server)
                {
                    double x, y, z;
                    float yaw, pitch;
                    bool g;
                    switch (packetID)
                    {
                        case 0x11:
                            x = readNextDouble(ref packetData);
                            y = readNextDouble(ref packetData);
                            z = readNextDouble(ref packetData);
                            g = readNextBool(ref packetData);
                            LogLine("[C -> S] Player Position: " + x + ", " + y + ", " + z + ", " + g);
                            break;
                        case 0x12:
                            x = readNextDouble(ref packetData);
                            y = readNextDouble(ref packetData);
                            z = readNextDouble(ref packetData);
                            readNextDouble(ref packetData); //skip 2 floats: look yaw & pitch
                            g = readNextBool(ref packetData);
                            LogLine("[C -> S] Player Position And Rotation: " + x + ", " + y + ", " + z + ", (look)" + ", " + g);
                            break;
                        case 0x13:
                            yaw = readNextFloat(ref packetData);
                            pitch = readNextFloat(ref packetData);
                            g = readNextBool(ref packetData);
                            LogLine("[C -> S] Player Rotation: " + yaw + ", " + pitch + ", " + g);
                            break;
                        case 0x15:
                            x = readNextDouble(ref packetData);
                            y = readNextDouble(ref packetData);
                            z = readNextDouble(ref packetData);
                            yaw = readNextFloat(ref packetData);
                            pitch = readNextFloat(ref packetData);
                            LogLine($"[C -> S] Vehicle Move: x: {x:0.0}, y: {y:0.0}, z: {z:0.0}, yaw: {yaw:0.0}, pitch: {pitch:0.0}");
                            break;
                        case 0x16:
                            bool b1 = readNextBool(ref packetData);
                            bool b2 = readNextBool(ref packetData);
                            LogLine($"[C -> S] Steer boat: b1: {b1}, b2: {b2}");
                            break;
                        case 0x1C:
                            float sideways = readNextFloat(ref packetData);
                            float forward = readNextFloat(ref packetData);
                            byte flags = readNextByte(ref packetData);
                            LogLine($"[C -> S] Steer Vehicle: sideways: {sideways}, forward: {forward}, flags: {flags}");
                            break;
                        case 0x09:
                            byte window = readNextByte(ref packetData);
                            short slot = readNextShort(ref packetData);
                            byte button = readNextByte(ref packetData);
                            short action = readNextShort(ref packetData);
                            int mode = readNextVarInt(ref packetData);
                            bool slotPresent = readNextBool(ref packetData);
                            int itemId = -1;
                            byte itemCount = 0;
                            if (slotPresent)
                            {
                                itemId = readNextVarInt(ref packetData);
                                itemCount = readNextByte(ref packetData);
                            }
                            LogLine("[C -> S] Window #" + window + " click: #" + slot + " button " + button + " action " + action + " mode " + mode + " item " + itemId + " x" + itemCount);
                            break;
                        default:
                            Log($"[C2S:0x{packetID:X}]");
                            break;
                    }
                }
                else
                {
                    switch (packetID)
                    {
                        case 0x35:
                            double x = readNextDouble(ref packetData);
                            double y = readNextDouble(ref packetData);
                            double z = readNextDouble(ref packetData);
                            readNextDouble(ref packetData); //skip 2 floats: look yaw & pitch
                            byte locMask = readNextByte(ref packetData);
                            LogLine("[S -> C] Player Position And Rotation: " + x + ", " + y + ", " + z + ", (look)" + ", " + locMask);
                            break;
                        default:
                            // Too much spam
                            Log($"[S2C:0x{packetID:X}]");
                            break;
                    }
                }
            }
        }

        public void Dispose()
        {
            try
            {
                client.Close();
                server.Close();
            }
            catch { }
        }

        private byte[] readDataRAW(TcpClient c, int offset)
        {
            if (offset > 0)
            {
                try
                {
                    byte[] cache = new byte[offset];
                    Receive(c, cache, 0, offset, SocketFlags.None);
                    return cache;
                }
                catch (OutOfMemoryException) { }
            }
            return new byte[] { };
        }

        private byte[] readData(int offset, ref byte[] cache)
        {
            List<byte> read = new List<byte>();
            List<byte> list = new List<byte>(cache);
            while (offset > 0 && list.Count > 0)
            {
                read.Add(list[0]);
                list.RemoveAt(0);
                offset--;
            }
            cache = list.ToArray();
            return read.ToArray();
        }

        private string readNextString(ref byte[] cache)
        {
            int length = readNextVarInt(ref cache);
            if (length > 0)
            {
                return Encoding.UTF8.GetString(readData(length, ref cache));
            }
            else return "";
        }

        private bool readNextBool(ref byte[] cache)
        {
            byte[] rawValue = readData(1, ref cache);
            return rawValue[0] != 0;
        }

        private byte readNextByte(ref byte[] cache)
        {
            byte[] rawValue = readData(1, ref cache);
            return rawValue[0];
        }

        private short readNextShort(ref byte[] cache)
        {
            byte[] rawValue = readData(2, ref cache);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToInt16(rawValue, 0);
        }

        private double readNextDouble(ref byte[] cache)
        {
            byte[] rawValue = readData(8, ref cache);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToDouble(rawValue, 0);
        }

        private float readNextFloat(ref byte[] cache)
        {
            byte[] rawValue = readData(4, ref cache);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToSingle(rawValue, 0);
        }

        private int readNextVarIntRAW(TcpClient c)
        {
            int i = 0;
            int j = 0;
            int k = 0;
            byte[] tmp = new byte[1];
            while (true)
            {
                Receive(c, tmp, 0, 1, SocketFlags.None);
                k = tmp[0];
                i |= (k & 0x7F) << j++ * 7;
                if (j > 5) throw new OverflowException("VarInt too big");
                if ((k & 0x80) != 128) break;
            }
            return i;
        }

        private int readNextVarInt(ref byte[] cache)
        {
            int i = 0;
            int j = 0;
            int k = 0;
            byte[] tmp = new byte[1];
            while (true)
            {
                tmp = readData(1, ref cache);
                k = tmp[0];
                i |= (k & 0x7F) << j++ * 7;
                if (j > 5) throw new OverflowException("VarInt too big");
                if ((k & 0x80) != 128) break;
            }
            return i;
        }

        private static byte[] getVarInt(int paramInt)
        {
            List<byte> bytes = new List<byte>();
            while ((paramInt & -128) != 0)
            {
                bytes.Add((byte)(paramInt & 127 | 128));
                paramInt = (int)(((uint)paramInt) >> 7);
            }
            bytes.Add((byte)paramInt);
            return bytes.ToArray();
        }

        private static byte[] concatBytes(params byte[][] bytes)
        {
            List<byte> result = new List<byte>();
            foreach (byte[] array in bytes)
                result.AddRange(array);
            return result.ToArray();
        }

        private void Receive(TcpClient c, byte[] buffer, int start, int offset, SocketFlags f)
        {
            int read = 0;
            while (read < offset)
                read += c.Client.Receive(buffer, start + read, offset - read, f);
        }
    }
}

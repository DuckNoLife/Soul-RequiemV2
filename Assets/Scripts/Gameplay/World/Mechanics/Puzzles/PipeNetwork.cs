using System.Collections.Generic;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Pure logic (no Unity) of the pipe grid - easy to test.
    /// Each cell is a 4-bit mask of open directions: N = 1, E = 2, S = 4, W = 8. Cell (x, y) has index y * cols + x, y = 0 is the top row.
    /// </summary>
    public static class PipeNetwork
    {
        /// <summary>Step in direction k (0 N, 1 E, 2 S, 3 W): dx, dy (y down), the direction's bit, the opposite bit.</summary>
        public static readonly int[] DX = { 0, 1, 0, -1 }, DY = { -1, 0, 1, 0 }, Bit = { 1, 2, 4, 8 }, Opposite = { 4, 8, 1, 2 };

        public static int RotateCW(int mask) => ((mask << 1) | (mask >> 3)) & 15;
        public static int RotateCCW(int mask) => ((mask >> 1) | (mask << 3)) & 15;

        /// <summary>"x,y,N|E|S|W" -> cell and direction bit. Bad format -> false.</summary>
        public static bool ParseEnd(string s, out int x, out int y, out int bit)
        {
            x = y = bit = 0;
            var p = (s ?? "").Split(',');
            if (p.Length != 3 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y)) return false;
            bit = p[2].Trim().ToUpperInvariant() switch { "N" => 1, "E" => 2, "S" => 4, _ => 8 };
            return true;
        }

        /// <summary>
        /// Flood fill from the INLET (sx, sy, sbit). <paramref name="wet"/> = cells carrying Ichor;
        /// <paramref name="ends"/> = open (cell, direction k) pairs whose other side does not connect back (out of the grid or a closed cell).
        /// </summary>
        public static void Flow(int[] masks, int cols, int rows, int sx, int sy, int sbit, HashSet<int> wet, List<(int cell, int k)> ends)
        {
            int s = sy * cols + sx;
            if (s < 0 || s >= masks.Length || (masks[s] & sbit) == 0) return;
            wet.Add(s);
            var q = new Queue<int>();
            q.Enqueue(s);
            while (q.Count > 0)
            {
                int i = q.Dequeue(), x = i % cols, y = i / cols;
                for (int k = 0; k < 4; k++)
                {
                    if ((masks[i] & Bit[k]) == 0) continue;
                    int nx = x + DX[k], ny = y + DY[k];
                    if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) { ends.Add((i, k)); continue; }
                    int j = ny * cols + nx;
                    if ((masks[j] & Opposite[k]) == 0) { ends.Add((i, k)); continue; }
                    if (wet.Add(j)) q.Enqueue(j);
                }
            }
        }
    }
}

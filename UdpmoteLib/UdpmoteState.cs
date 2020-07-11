using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdpmoteLib
{
    public class UdpmoteState
    {
        public ButtonState ButtonState { get; private set; }
        public AccelState AccelState { get; private set; }
        public float PointerX { get; set; }
        public float PointerY { get; set; }

        public int NumMote { get; private set; }

        public UdpmoteState(byte[] data)
        {
            NumMote = data[1];
            if (BitConverter.IsLittleEndian)
            {
                Swap(data, 3);
                Swap(data, 7);
                Swap(data, 11);
                Swap(data, 15);
                Swap(data, 19);
                Swap(data, 23);
            }
            AccelState = new AccelState(
                BitConverter.ToInt32(data, 3) / SCALE,
                BitConverter.ToInt32(data, 7) / SCALE,
                BitConverter.ToInt32(data, 11) / SCALE);

            var mask = BitConverter.ToUInt32(data, 15);
            ButtonState = new ButtonState(mask);

            PointerX = BitConverter.ToInt32(data, 19) / SCALE;
            PointerY = BitConverter.ToInt32(data, 23) / SCALE;
        }

        #region Private

        private const float SCALE = 1024 * 1024;

        private void Swap(byte[] data, int s)
        {
            var aux = data[s];
            data[s] = data[s + 3];
            data[s + 3] = aux;
            aux = data[s + 1];
            data[s + 1] = data[s + 2];
            data[s + 2] = aux;
        }

        private UInt32 GetUInt32BigEndian(byte[] data, int s)
        {
            return ((uint)data[s]) << 24; // | data[s + 1] << 16 | data[s + 2] << 8 | data[s + 4];
        }

        #endregion
    }
    public struct AccelState
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }

        public AccelState(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return string.Format("AccelState({0:0.00}, {1:0.00}, {2:0.00})", X, Y, Z);
        }
    }

    public struct ButtonState
    {
        public bool A { get; private set; }
        public bool B { get; private set; }
        public bool Plus { get; private set; }
        public bool Home { get; private set; }
        public bool Minus { get; private set; }
        public bool One { get; private set; }
        public bool Two { get; private set; }
        public bool Up { get; private set; }
        public bool Down { get; private set; }
        public bool Left { get; private set; }
        public bool Right { get; private set; }

        public ButtonState(uint mask)
        {
            One = (mask & (1 << 0)) != 0;
            Two = (mask & (1 << 1)) != 0;
            A = (mask & (1 << 2)) != 0;
            B = (mask & (1 << 3)) != 0;

            Plus = (mask & (1 << 4)) != 0;
            Minus = (mask & (1 << 5)) != 0;
            Home = (mask & (1 << 6)) != 0;

            Up = (mask & (1 << 7)) != 0;
            Down = (mask & (1 << 8)) != 0;
            Left = (mask & (1 << 9)) != 0;
            Right = (mask & (1 << 10)) != 0;
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.Append("PressedButtons: {");
            bool first = true;
            foreach (var prop in this.GetType().GetProperties())
            {
                if ((bool)prop.GetValue(this))
                {
                    if (first)
                    {
                        first = false;
                        s.Append(prop.Name);
                    }
                    else
                        s.Append(", " + prop.Name);
                }
            }
            s.Append("}");
            return s.ToString();
        }
    }
}

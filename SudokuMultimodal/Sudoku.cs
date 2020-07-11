using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SudokuMultimodal
{
    public class Sudoku
    {
        public event Action<int, int, int> CeldaCambiada; //fila, columna, nuevoNúmero

        public const int Tamaño = 9;

        public int this[int fil, int col]
        {
            get { return _números[fil, col]; }
            set
            {
                if (esInicial[fil, col])
                    return; //los iniciales no se pueden cambiar
                _números[fil, col] = value;
                CeldaCambiada(fil, col, value);
            }
        }

        public Sudoku()
        {
            CeldaCambiada += (fila, col, num) => { };
            _números = new int[Tamaño, Tamaño] { 
                {0,7,4,0,8,0,5,0,0},
                {0,0,3,0,7,0,0,0,4},
                {6,0,0,0,0,0,0,3,1},
                {0,0,0,9,0,2,0,0,0},
                {4,9,0,0,0,0,0,2,6},
                {0,0,0,5,0,6,0,0,0},
                {5,3,0,0,0,0,0,0,7},
                {8,0,0,0,6,0,1,0,0},
                {0,0,2,0,5,0,8,4,0},
            };

            esInicial = new bool[Tamaño, Tamaño];
            for (int f = 0; f < Tamaño; ++f)
                for (int c = 0; c < Tamaño; ++c)
                    esInicial[f, c] = _números[f, c] > 0;

            /* //Comprueba FilaColumnaACuadrantePosicion y CuadrantePosicionAFilaColumna
            for (int i = 0; i < Tamaño/3; ++i)
                for (int j = 0; j < Tamaño/3; ++j)
                {
                    int c, p;
                    FilaColumnaACuadrantePosicion(i, j, out c, out p);
                    int i2, j2;
                    CuadrantePosicionAFilaColumna(c, p, out i2, out j2);
                    if (i != i2 || j != j2)
                        Console.WriteLine("ERROR");
                    //Console.WriteLine(string.Format("{0},{1} => {2},{3} => {4},{5}", i, j, c, p, i2, j2));
                }
            */
            Console.WriteLine("------------------");
            foreach (var item in PosiblesEnCelda(4, 4))
                Console.WriteLine(item);
        }

        #region public
        
        public List<int> PosiblesEnCelda(int fila, int col)
        {
            List<int> res = new List<int>();
            if (_números[fila, col] != 0) { res.Add(_números[fila, col]); return res; }
            var libres = new bool[Tamaño];
            for (int i = 0; i < Tamaño; ++i) libres[i] = true;
            int cuad, pos;
            FilaColumnaACuadrantePosicion(fila, col, out cuad, out pos);
            for (int i = 0; i < Tamaño; ++i)
            {
                if (_números[fila, i] != 0) libres[_números[fila, i] - 1] = false;
                if (_números[i, col] != 0) libres[_números[i, col] - 1] = false;
                int f, c;
                CuadrantePosicionAFilaColumna(cuad, i, out f, out c);
                if (_números[f, c] != 0) libres[_números[f, c] - 1] = false;
            }
            for (int i = 0; i < Tamaño; ++i)
                if (libres[i])
                    res.Add(i + 1);
            return res;
        }

        public void Reiniciar()
        {
            for (int f = 0; f < Tamaño; ++f)
                for (int c = 0; c < Tamaño; ++c)
                    if (!esInicial[f, c])
                        this[f, c] = 0;
        }

        public static void FilaColumnaACuadrantePosicion(int fil, int col, out int cuad, out int pos)
        {
            cuad = col / 3 + (fil / 3) * 3;
            pos = (fil % 3) * 3 + col % 3;
        }

        public static void CuadrantePosicionAFilaColumna(int cuad, int pos, out int fil, out int col)
        {
            fil = (pos / 3) + (cuad / 3) * 3;
            col = (cuad % 3) * 3 + pos % 3;
        }

        #endregion

        #region private

        int[,] _números;
        bool[,] esInicial;

        #endregion
    }
}

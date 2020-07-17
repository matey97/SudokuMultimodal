using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SudokuMultimodal
{
    public enum SudokuLevel { EASY=1, MEDIUM=2, HARD=3};
    public class Sudoku
    {
        public event Action<int, int, int> CeldaCambiada; //fila, columna, nuevoNúmero
        public event Action SudokuSolved;
        public event Action SudokuWrong;

        private const string BASE_URI = "http://www.cs.utep.edu/cheon/ws/sudoku/new/";
        private const string PARAMS = "?size=9&level={0}";
        public const int Tamaño = 9;

        private HttpClient client;
        private int remainingNumbers = 81;


        public int this[int fil, int col]
        {
            get { return _números[fil, col]; }
            set
            {
                if (esInicial[fil, col])
                    return; //los iniciales no se pueden cambiar

                if (_números[fil, col] == 0 && value != 0)
                    remainingNumbers--;
                else if (_números[fil, col] != 0 && value == 0)
                    remainingNumbers++;

                _números[fil, col] = value;
                CeldaCambiada(fil, col, value);

                if (remainingNumbers == 0)
                    CheckSudoku();
            }
        }

        public Sudoku(SudokuLevel level, Action SudokuLoaded)
        {
            CeldaCambiada += (fila, col, num) => { };

            if (client == null)
            {
                client = new HttpClient();
                client.BaseAddress = new Uri(BASE_URI);
            }

            var downloadTask = Task.Factory.StartNew(() =>
            {
                _números = GetNewSudoku(level);
            });

            downloadTask.ContinueWith(new Action<Task>((t) =>
            {
                esInicial = new bool[Tamaño, Tamaño];
                for (int f = 0; f < Tamaño; ++f)
                    for (int c = 0; c < Tamaño; ++c)
                        esInicial[f, c] = _números[f, c] > 0;
                SudokuLoaded();
            }), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private int[,] GetNewSudoku(SudokuLevel level)
        {
            var request = client.GetAsync(string.Format(PARAMS, (int)level));
            request.Wait();

            var response = request.Result;
            response.EnsureSuccessStatusCode();

            var jsonStringAsync = response.Content.ReadAsStringAsync();
            jsonStringAsync.Wait();

            var jsonString = jsonStringAsync.Result;
            var sudokuNumbers = JObject.Parse(jsonString)["squares"];

            var numbers = new int[Tamaño, Tamaño];
            foreach (var number in sudokuNumbers)
            {
                numbers[(int)number["y"], (int)number["x"]] = (int)number["value"];
                remainingNumbers--;
            }

            return numbers;
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

        private void CheckSudoku()
        {
            if (AreValidRows() && AreValidColumns() && AreValidQuadrants())
                SudokuSolved?.Invoke();
            else
                SudokuWrong?.Invoke();
        }

        private bool AreValidRows()
        {
            HashSet<int> rowSet; // Contains mas barato que en lista.
            int currentNumber;
            for (var row = 0; row < Tamaño; row++)
            {
                rowSet = new HashSet<int>();
                for (var col = 0; col < Tamaño; col++)
                {
                    currentNumber = _números[row, col];
                    if (rowSet.Contains(currentNumber))
                        return false;
                    rowSet.Add(currentNumber);
                }
            }

            return true;
        }

        private bool AreValidColumns()
        {
            HashSet<int> colSet; // Contains mas barato que en lista.
            int currentNumber;
            for (var col = 0; col < Tamaño; col++)
            {
                colSet = new HashSet<int>();
                for (var row = 0; row < Tamaño; row++)
                {
                    currentNumber = _números[row, col];
                    if (colSet.Contains(currentNumber))
                        return false;
                    colSet.Add(currentNumber);
                }
            }

            return true;
        }

        private bool AreValidQuadrants()
        {
            HashSet<int> quadrantSet;
            int currentNumber, row, col;

            for (var quad = 0; quad < Tamaño; quad++)
            {
                quadrantSet = new HashSet<int>();
                for (var pos = 0; pos < Tamaño; pos++)
                {
                    CuadrantePosicionAFilaColumna(quad, pos, out row, out col);
                    currentNumber = _números[row, col];
                    if (quadrantSet.Contains(currentNumber))
                        return false;
                    quadrantSet.Add(currentNumber);
                }
            }

            return true;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using UdpmoteGestureLib;
using UdpmoteLib;

namespace SudokuMultimodal
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Loaded += new RoutedEventHandler(MainWindow_Loaded);
            InitializeComponent();
        }

        #region private

        Sudoku _s;
        Cuadrante[] _cuadrantes;
        UniformGrid _ug;
        int _filaActual, _columnaActual;
        bool mostrarPosibles;

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mostrarPosibles = false;
            KeyDown += new KeyEventHandler(MainWindow_KeyDown);

            _ug = new UniformGrid() { Rows = Sudoku.Tamaño / 3, Columns = Sudoku.Tamaño / 3, Background = Brushes.WhiteSmoke };
            mainGrid.Children.Add(_ug);
            _cuadrantes = new Cuadrante[Sudoku.Tamaño];

            SetupUdpmote();
            SetupNumbersPopup();

            SpeechRecognitionService.GetInstance().SetGrammar(GrammarType.MOUSE_VOICE);

            NuevaPartida();
        }

        void NuevaPartida() //Mientras no cambiemos el constructor de Sudoku siempre es la misma partida
        {
            _filaActual = _columnaActual = -1;
            _s = new Sudoku();
            _s.CeldaCambiada += CuandoCeldaCambiada;

            ActualizarVistaSudoku();

            //Copio a mano algunos números de la solución: el sudoku elegido es dificil ;)
            _s[0, 0] = 2;
            _s[4, 4] = 1;
            _s[7, 7] = 5;
            _s[1, 7] = 8;
            _s[7, 1] = 4;
            _s[3, 2] = 6;
            _s[5, 6] = 9;
        }

        void ReiniciarPartida()
        {
            _s.Reiniciar();
            ActualizarVistaSudoku();
        }

        void ActualizarVistaSudoku()
        {
            _ug.Children.Clear();

            for (int cuad = 0; cuad < Sudoku.Tamaño; ++cuad)
            {
                var cuadrante = new Cuadrante(_s, cuad, SolicitudCambioNúmero, SolicitudSeleccionada, RequestNumbersPopup);
                _cuadrantes[cuad] = cuadrante;
                _ug.Children.Add(cuadrante.UI);
            }

            ActualizaPosibles();

            PonSelecciónEn(0, 0);
        }

        void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    {
                        _s[_filaActual, _columnaActual] = 0;
                        ActualizaPosibles();
                    }
                    break;
                case Key.Up:
                    if (_filaActual > 0)
                        PonSelecciónEn(_filaActual - 1, _columnaActual);
                    break;
                case Key.Down:
                    if (_filaActual < Sudoku.Tamaño - 1)
                        PonSelecciónEn(_filaActual + 1, _columnaActual);
                    break;
                case Key.Left:
                    if (_columnaActual > 0)
                        PonSelecciónEn(_filaActual, _columnaActual - 1);
                    break;
                case Key.Right:
                    if (_columnaActual < Sudoku.Tamaño - 1)
                        PonSelecciónEn(_filaActual, _columnaActual + 1);
                    break;
                case Key.D1:
                case Key.NumPad1:
                case Key.D2:
                case Key.NumPad2:
                case Key.D3:
                case Key.NumPad3:
                case Key.D4:
                case Key.NumPad4:
                case Key.D5:
                case Key.NumPad5:
                case Key.D6:
                case Key.NumPad6:
                case Key.D7:
                case Key.NumPad7:
                case Key.D8:
                case Key.NumPad8:
                case Key.D9:
                case Key.NumPad9:
                    {
                        _s[_filaActual, _columnaActual] = int.Parse(new string(e.Key.ToString()[1], 1));
                        ActualizaPosibles();
                        e.Handled = true;
                    }
                    break;
                default:
                    break;
            }
        }

        void PonSelecciónEn(int fil, int col)
        {
            if (_filaActual >= 0 && _columnaActual >= 0)
            {
                Sudoku.FilaColumnaACuadrantePosicion(_filaActual, _columnaActual, out int cuad, out int pos);
                _cuadrantes[cuad].DeseleccionaCelda(pos);
            }

            Sudoku.FilaColumnaACuadrantePosicion(fil, col, out int cuad2, out int pos2);
            _cuadrantes[cuad2].SeleccionaCelda(pos2);
            _filaActual = fil;
            _columnaActual = col;

            numbersPopup.IsOpen = false;
        }

        void ActualizaPosibles()
        {
            for (int cuad = 0; cuad < Sudoku.Tamaño; ++cuad)
                _cuadrantes[cuad].QuitaTodosPosibles();

            if (!mostrarPosibles) return;

            for (int f = 0; f < Sudoku.Tamaño; ++f)
                for (int c = 0; c < Sudoku.Tamaño; ++c)
                {
                    Sudoku.FilaColumnaACuadrantePosicion(f, c, out int cuad, out int pos);
                    foreach (var num in _s.PosiblesEnCelda(f, c))
                        _cuadrantes[cuad].PonerPosibleEnPos(pos, num);
                }
        }

        void CuandoCeldaCambiada(int fila, int columna, int nuevoNúmero)
        {
            Sudoku.FilaColumnaACuadrantePosicion(fila, columna, out int cuad, out int pos);
            //añade el número
            if (nuevoNúmero > 0)
                _cuadrantes[cuad].PonerNúmeroEnPos(pos, nuevoNúmero);
            else
                _cuadrantes[cuad].QuitarNúmeroEnPos(pos);
            //Actualiza posibles
            ActualizaPosibles();
        }

        void SolicitudCambioNúmero(int fila, int col, int número)
        {
            // El operador [] está sobrecargado. Llama al método 'public int this[int fil, int col]' de la clase Sudoku.

            _s[fila, col] = número;
        }

        void SolicitudSeleccionada(int fila, int col)
        {
            PonSelecciónEn(fila, col);
        }

        void BotónNuevoClick(object sender, RoutedEventArgs e)
        {
            NuevaPartida();
        }

        void BotónReiniciarClick(object sender, RoutedEventArgs e)
        {
            ReiniciarPartida();
        }

        void CheckboxVerPosiblesClick(object sender, RoutedEventArgs e)
        {
            mostrarPosibles = (sender as CheckBox).IsChecked == true;
            ActualizaPosibles();
        }

        #endregion

        #region Udpmote

        private const string GESTURE_FILE = "\\sudoku_gestures.bin";
        private const string DELETE = "BORRAR", NEW = "NUEVO", RESTART = "REINICIAR", SHOW_POSSIBLE = "POSIBLES";

        private Udpmote udpmote;
        private GestureCapturer gestureCapturer;
        private GestureRecognizer gestureRecognizer;
        private System.Timers.Timer timerEnabler;

        private bool isMovingEnabled = true;


        private void SetupUdpmote()
        {
            udpmote = new Udpmote();
            gestureCapturer = new GestureCapturer();
            gestureRecognizer = new GestureRecognizer();

            //Enlazamos eventos
            udpmote.UdpmoteChanged += Udpmote_UdpmoteChanged;
            udpmote.UdpmoteChanged += gestureCapturer.OnUdpmoteChanged;
            gestureCapturer.GestureCaptured += gestureRecognizer.OnGestureCaptured;
            gestureRecognizer.GestureRecognized += GestureRecognizer_GestureRecognized;

            LoadGestures();

            // Este timer evita que la celda seleccionada se desplace varias veces con una única pulsación
            SetupTimerEnabler();

            udpmote.Connect();
        }

        private void LoadGestures()
        {
            using (Stream stream = new FileStream(Directory.GetCurrentDirectory() + GESTURE_FILE, FileMode.Open, FileAccess.Read))
            {
                BinaryFormatter bf = new BinaryFormatter();
                var prototypes = (List<Gesture>)bf.Deserialize(stream);
                foreach (Gesture prototype in prototypes)
                    gestureRecognizer.AddPrototypes(prototype);
            }
        }

        private void SetupTimerEnabler()
        {
            timerEnabler = new System.Timers.Timer(100);
            timerEnabler.Elapsed += TimerEnabler_Elapsed;
        }

        private void TimerEnabler_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            timerEnabler.Stop();
            isMovingEnabled = true;
        }

        private void Udpmote_UdpmoteChanged(UdpmoteState state)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<ButtonState>(CheckUdpmoteButtons), state.ButtonState);
        }

        private void CheckUdpmoteButtons(ButtonState state)
        {
            //Cuando se presiona una flecha de dirección se mueve la celda seleccionada, 
            // se desactiva el movimiento y se activa el timer que se encarga de volver a activar el movimiento
            if (isMovingEnabled)
            {
                timerEnabler.Start();
                if (state.Up && _filaActual > 0)
                    PonSelecciónEn(_filaActual - 1, _columnaActual);
                else if (state.Down && _filaActual < Sudoku.Tamaño - 1)
                    PonSelecciónEn(_filaActual + 1, _columnaActual);
                else if (state.Left && _columnaActual > 0)
                    PonSelecciónEn(_filaActual, _columnaActual - 1);
                else if (state.Right && _columnaActual < Sudoku.Tamaño - 1)
                    PonSelecciónEn(_filaActual, _columnaActual + 1);
                isMovingEnabled = false;
            }  
        }

        private void GestureRecognizer_GestureRecognized(string gestureName)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<string>(HandleGesture), gestureName);
        }

        private void HandleGesture(string gestureName)
        {
            var isNumeric = int.TryParse(gestureName, out int number);
            Console.WriteLine(gestureName);
            if (isNumeric)
            {
                _s[_filaActual, _columnaActual] = number;
                ActualizaPosibles();
            } else
            {
                switch (gestureName)
                {
                    case DELETE:
                        _s[_filaActual, _columnaActual] = 0;
                        ActualizaPosibles();
                        break;
                    case NEW:
                        NuevaPartida();
                        break;
                    case RESTART:
                        ReiniciarPartida();
                        break;
                    case SHOW_POSSIBLE:
                        CB_VerPosibles.IsChecked = !CB_VerPosibles.IsChecked;
                        mostrarPosibles = CB_VerPosibles.IsChecked == true;
                        ActualizaPosibles();
                        break;
                }
            }
        }

        #endregion

        #region OnlyMouse(Popup)
        private const int RADIUS = 46;
        private const int ANGLE_SEPARATION = 36; // 360º/10
        private const int ANGLE_OFFSET = 180;

        private Popup numbersPopup = new Popup()
        {
            Width = 120,
            Height = 122,
            IsOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Placement = PlacementMode.Center //Magia
        };
        private Canvas numbersCanvas = new Canvas()
        {
            Width = 120,
            Height = 122
        };

        private void SetupNumbersPopup()
        {
            Ellipse number;
            for (var i = 0; i < 10; i++)
            {
                number = new Ellipse
                {
                    Width = 30,
                    Height = 30,
                    Stroke = Brushes.Black,
                    Fill = Brushes.Blue
                };
                number.Name = i == 0 ? "X" : "Number_" + i;

                (double x, double y) = GetNumberCoordinates(i);

                TextBlock textNumber = new TextBlock()
                {
                    Text = i == 0 ? "X" : i.ToString(),
                    FontSize = 20,
                    Foreground = Brushes.White
                };

                number.MouseDown += Number_MouseDown;
                textNumber.MouseDown += TextNumber_MouseDown;

                Canvas.SetTop(number, x);
                Canvas.SetLeft(number, y);
                Canvas.SetTop(textNumber, x);
                Canvas.SetLeft(textNumber, y + 10);

                numbersCanvas.Children.Add(number);
                numbersCanvas.Children.Add(textNumber);
            }
            
            numbersPopup.Child = numbersCanvas;
        }

        private void Number_MouseDown(object sender, MouseButtonEventArgs e)
        {
            numbersPopup.IsOpen = false;
            string result = ((Ellipse)sender).Name;

            if (result.Equals("X"))
                result = "0";
            else
                result = result.Split('_')[1];

            _s[_filaActual, _columnaActual] = int.Parse(result);
        }

        private void TextNumber_MouseDown(object sender, MouseButtonEventArgs e)
        {
            numbersPopup.IsOpen = false;
            string result = ((TextBlock)sender).Text;

            if (result.Equals("X"))
                result = "0";

            _s[_filaActual, _columnaActual] = int.Parse(result);
        }

        private (double, double) GetNumberCoordinates(int i)
        {
            double x = Math.Cos((Math.PI / 180) * (ANGLE_SEPARATION * -i + ANGLE_OFFSET)) * RADIUS;
            double y = Math.Sin((Math.PI / 180) * (ANGLE_SEPARATION * -i + ANGLE_OFFSET)) * RADIUS;

            return (x + RADIUS, y + RADIUS);
        }

        private void RequestNumbersPopup(UIElement element)
        {
            numbersPopup.PlacementTarget = element;
            numbersPopup.IsOpen = true;
        }

        #endregion
    }
}

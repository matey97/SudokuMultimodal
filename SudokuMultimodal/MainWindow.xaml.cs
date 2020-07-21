using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Speech.Recognition;
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
using System.Windows.Media.Animation;
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
        SudokuLevel level;

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mostrarPosibles = false;
            KeyDown += new KeyEventHandler(MainWindow_KeyDown);

            _ug = new UniformGrid() { Rows = Sudoku.Tamaño / 3, Columns = Sudoku.Tamaño / 3, Background = Brushes.WhiteSmoke };
            mainGrid.Children.Add(_ug);
            _cuadrantes = new Cuadrante[Sudoku.Tamaño];

            SetupUdpmote();
            SetupNumbersPopup();
            SetupHeaders();
            SpeechRecognitionService.GetInstance().SetGrammar(GrammarType.MOUSE_VOICE);

            NuevaPartida();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            level = RB_Easy.IsChecked == true ? 
                        SudokuLevel.EASY : 
                        RB_Med.IsChecked == true ? 
                            SudokuLevel.MEDIUM : 
                            SudokuLevel.HARD;
        }

        void NuevaPartida()
        {
            ShowSpinner();
            _filaActual = _columnaActual = -1;
            _s = new Sudoku(level, SudokuLoaded);
        }

        // Método que llama el sudoku cuando esta listo
        private void SudokuLoaded()
        {
            HideSpinner();
            _s.CeldaCambiada += CuandoCeldaCambiada;
            _s.SudokuSolved += _s_SudokuSolved;
            _s.SudokuWrong += _s_SudokuWrong;
            ActualizarVistaSudoku();
        }

        private void _s_SudokuSolved()
        {
            var result = MessageBox.Show("Enhorabuena, Sudoku Completado!\n¿Deseas iniciar una nueva partida?", "Sudoku Multimodal", MessageBoxButton.YesNo, MessageBoxImage.Information);
            
            if (result == MessageBoxResult.Yes)
                NuevaPartida();
        }


        private void _s_SudokuWrong()
        {
            MessageBox.Show("Ups! El sudoku es incorrecto", "Sudoku Multimodal", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private bool areButtonsEnabled = true;


        private void SetupUdpmote()
        {
            udpmote = new Udpmote();
            gestureCapturer = new GestureCapturer();
            gestureRecognizer = new GestureRecognizer();

            // Enlazamos eventos de udpmote, capturador y reconocedor
            udpmote.UdpmoteChanged += Udpmote_UdpmoteChanged;
            udpmote.UdpmoteChanged += gestureCapturer.OnUdpmoteChanged;
            gestureCapturer.GestureCaptured += gestureRecognizer.OnGestureCaptured;
            gestureRecognizer.GestureRecognized += GestureRecognizer_GestureRecognized;

            LoadGestures();
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
            areButtonsEnabled = true;
        }

        private void Udpmote_UdpmoteChanged(UdpmoteState state)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<ButtonState>(CheckUdpmoteButtons), state.ButtonState);
        }

        private void CheckUdpmoteButtons(ButtonState state)
        {
            // Cuando se presiona una flecha de dirección o los botones +/- 
            // se "desactivan" los botones y se inicia el timer que se encarga de volver a "activar" los botones.
            // Esto evita que la selección de la celda se mueva varias veces o la dificultad aumente/disminuya varias veces con una sola pulsación.
            if (areButtonsEnabled && (state.Up || state.Left || state.Right || state.Down || state.Minus || state.Plus))
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

                if (state.Minus)
                {
                    _ = RB_Med.IsChecked == true ?
                        RB_Easy.IsChecked = true :
                        RB_Hard.IsChecked == true ?
                            RB_Med.IsChecked = true :
                            RB_Easy.IsChecked = true;
                }

                if (state.Plus)
                {
                    _ = RB_Med.IsChecked == true ?
                        RB_Hard.IsChecked = true :
                        RB_Easy.IsChecked == true ?
                            RB_Med.IsChecked = true :
                            RB_Hard.IsChecked = true;
                }

                areButtonsEnabled = false;
            }  
        }

        private void GestureRecognizer_GestureRecognized(string gestureName)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<string>(HandleGesture), gestureName);
        }

        private void HandleGesture(string gestureName)
        {
            var isNumeric = int.TryParse(gestureName, out int number);
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
        private const int ANGLE_SEPARATION = 36; // 360º / 10 opciones
        private const int ANGLE_OFFSET = 180; // Por como funcionan las coordenadas en el canvas, se aplica este offset para que el rosco quede bien ordenado

        private Popup numbersPopup = new Popup()
        {
            Width = 120,
            Height = 122,
            IsOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.None,
            Placement = PlacementMode.Center // El popup queda centrado con el elemento target, parece magia.
        };
        private Canvas numbersCanvas = new Canvas()
        {
            Width = 120,
            Height = 122
        };

        // Crea el rosco de numeros:
        // El rosco esta formado por elipses y bloques de texto
        // Hay que suscribirse al MouseDown tanto de las elipses como de los bloques de texto
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

        // Dado un ángulo, devuelve las coordenadas correspondientes en la circunferencia del círculo
        private (double, double) GetNumberCoordinates(int i)
        {
            double x = Math.Cos((Math.PI / 180) * (ANGLE_SEPARATION * -i + ANGLE_OFFSET)) * RADIUS;
            double y = Math.Sin((Math.PI / 180) * (ANGLE_SEPARATION * -i + ANGLE_OFFSET)) * RADIUS;

            return (x + RADIUS, y + RADIUS);
        }

        // Las casillas emplean este método para solicitar que se abra el popup sobre ellas
        // El método recibe la casilla seleccionada y hace que sea el target del popup
        private void RequestNumbersPopup(UIElement element)
        {
            numbersPopup.PlacementTarget = element;
            numbersPopup.IsOpen = true;
        }

        #endregion

        #region OnlyVoice
        private const string TOP_HEADER = "123456789", LEFT_HEADER = "ABCDEFGHI";
        private const string NEW_KEY = "NewSudoku", RESTART_KEY = "Restart", PROBABLE_KEY = "SeeProbable", NUMBER_KEY = "Number", LEVEL_KEY = "Level";

        private SpeechRecognitionService speechRecognitionService = SpeechRecognitionService.GetInstance();
        private UniformGrid topHeader, leftHeader;

        // Crea los headers con las coordenadas de las casillas de los sudokus
        // Se muestran solo cuando el modo Solo Voz se activa
        private void SetupHeaders()
        {
            topHeader = CreateHeader("TOP");
            leftHeader = CreateHeader("LEFT");

            Grid.SetRow(topHeader, 0);
            Grid.SetRow(leftHeader, 1);
            Grid.SetColumn(topHeader, 1);
            Grid.SetColumn(leftHeader, 0);
        }

        private UniformGrid CreateHeader(string type)
        {
            bool isTop = type.Equals("TOP");
            string headerValues = isTop ? TOP_HEADER : LEFT_HEADER;

            UniformGrid uniformGrid = new UniformGrid()
            {
                Rows = isTop ? 1 : 9,
                Columns = isTop ? 9 : 1
            };

            Thickness margin = isTop ? new Thickness(0, 0, 0, 10) : new Thickness(0, 0, 15, 0);
            foreach (var c in headerValues)
                uniformGrid.Children.Add(new TextBlock()
                {
                    Text = c.ToString(),
                    FontFamily = new FontFamily("Comic Sans MS"),
                    FontSize = 40,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = margin
                });
            
            return uniformGrid;
        }

        private void ShowHeaders()
        {
            superGrid.Children.Add(topHeader);
            superGrid.Children.Add(leftHeader);
        }

        private void HideHeaders()
        {
            superGrid.Children.Remove(topHeader);
            superGrid.Children.Remove(leftHeader);
        }

        private void SpeechRecognitionService_SpeechRecognized(System.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            SemanticValue semantics = e.Result.Semantics;

            if (semantics.ContainsKey(NEW_KEY))
            {
                if (semantics.ContainsKey(LEVEL_KEY))
                {
                    switch (semantics[LEVEL_KEY].Value.ToString())
                    {
                        case "facil":
                            RB_Easy.IsChecked = true;
                            break;
                        case "media":
                            RB_Med.IsChecked = true;
                            break;
                        case "dificil":
                            RB_Hard.IsChecked = true;
                            break;
                    }
                }
                NuevaPartida();
            } 
            else if (semantics.ContainsKey(RESTART_KEY))
                ReiniciarPartida();
            else if (semantics.ContainsKey(PROBABLE_KEY))
            {
                CB_VerPosibles.IsChecked = mostrarPosibles = bool.Parse(semantics[PROBABLE_KEY].Value.ToString());
                ActualizaPosibles();
            } else
            {
                int newNumber = 0;

                if (semantics.ContainsKey(NUMBER_KEY))
                    newNumber = int.Parse(semantics[NUMBER_KEY].Value.ToString());

                int row = LEFT_HEADER.IndexOf(semantics["Row"].Value.ToString());
                int column = int.Parse(semantics["Column"].Value.ToString()) - 1;
                
                PonSelecciónEn(row, column);
                _s[_filaActual, _columnaActual] = newNumber;
            }
        }

        // Cuando se activa el modo Solo Voz, muestra los headers, se carga la gramática adecuada en el reconocedor, se suscribe, solicita activar el reconocimientos y desactiva el resto de modos de entrada
        // Cuando se desactiva el modo Solo Voz, oculta los headers, se deteniene el reconocimiento, se desuscribe, carga la gramática anterior, activa el resto de modos de entrada.
        private void CB_SoloVoz_Click(object sender, RoutedEventArgs e)
        {
            if (CB_SoloVoz.IsChecked == true)
            {
                speechRecognitionService.SetGrammar(GrammarType.ONLY_VOICE);
                speechRecognitionService.SpeechRecognized += SpeechRecognitionService_SpeechRecognized;
                speechRecognitionService.RequestEnableRecognition();
                ShowHeaders();
                DisableOtherInputMethods();
            } else
            {
                speechRecognitionService.RequestDisableRecognition();
                speechRecognitionService.SpeechRecognized -= SpeechRecognitionService_SpeechRecognized;
                speechRecognitionService.SetGrammar(GrammarType.MOUSE_VOICE);
                HideHeaders();
                EnableOtherInputMethods();
            }
        }

        private void EnableOtherInputMethods()
        {
            KeyDown += MainWindow_KeyDown;
            udpmote.UdpmoteChanged += Udpmote_UdpmoteChanged;
            EnableInputMethodsInQuadrantCells();
        }

        private void DisableOtherInputMethods()
        {
            KeyDown -= MainWindow_KeyDown;
            udpmote.UdpmoteChanged -= Udpmote_UdpmoteChanged;
            DisableInputMethodsInQuadrantCells();
        }

        private void EnableInputMethodsInQuadrantCells()
        {
            foreach (var quadrant in _cuadrantes)
            {
                quadrant.EnableInputMethodsInCells();
            }
        }

        private void DisableInputMethodsInQuadrantCells()
        {
            foreach (var quadrant in _cuadrantes)
            {
                quadrant.DisableInputMethodsInCells();
            }
        }
        #endregion

        #region Spinner
       
        // Tanto la carga del spinner como su animación se encuentran en el xaml
        private void ShowSpinner()
        {
            IM_Spinner.Visibility = Visibility.Visible;
            dockPanel.Visibility = Visibility.Collapsed;

        }

        private void HideSpinner()
        {
            IM_Spinner.Visibility = Visibility.Collapsed;
            dockPanel.Visibility = Visibility.Visible;
        }

        #endregion
    }
}

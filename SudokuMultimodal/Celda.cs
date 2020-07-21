using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.IO;
using Microsoft.Ink;
using System.Text.RegularExpressions;
using System.Speech.Recognition;
using System.Media;
using SudokuMultimodal.Properties;
using System.Windows.Shapes;

namespace SudokuMultimodal
{
    public class Celda
    {
        public Border UI { get; private set; }

        public bool EstáSeleccionada
        {
            get { return _estáSeleccionado; }
            set
            {
                _estáSeleccionado = value;
                selecciónBorde.Visibility = _estáSeleccionado ? Visibility.Visible : Visibility.Hidden;
            }
        }

        // solicitudCambioNúmero: Cuando la celda solicita cambiar el número que contiene (p.e. la tinta reconocida).
        // solicitudSeleccionada: Cuando la celda solicita ser la seleccionada.
        public Celda(int número, Action<int> solicitudCambioNúmero, Action solicitudSeleccionada, Action<UIElement> requestNumbersPopup)
        {
            _solicitudCambioNúmero = solicitudCambioNúmero;
            _solicitudSeleccionada = solicitudSeleccionada;
            _requestNumbersPopup = requestNumbersPopup;
            UI = new Border() { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5), Background=Brushes.Transparent };
            UI.PreviewMouseDown += new System.Windows.Input.MouseButtonEventHandler(UI_MouseDown);
            UI.PreviewMouseMove += UI_PreviewMouseMove;
            UI.MouseUp += UI_MouseUp;
            var grid = new Grid();
            UI.Child = grid;

            grid.Children.Add(_uniformGrid);
            for (int i = 0; i < Sudoku.Tamaño; ++i)
                _uniformGrid.Children.Add(new TextBlock()
                {
                    FontFamily = _fuente,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            grid.Children.Add(_textBlock);
            grid.Children.Add(selecciónBorde);

            grid.Children.Add(inkCanvas);
            SetupInkCanvas();

            SetupVoice();

            _modificable = número == 0;
            _textBlock.Foreground = _modificable ? Brushes.Blue : Brushes.Black;
            if (número != 0)
                ForzarPonerNúmero(número);
        }

        // Cuando se presiona el botón izquierdo se activa el timer de Raton+Voz y el modo edición del inkCanvas
        // Cuando se presiona el botón derecho se solicita el popup en la casilla correspondiente
        void UI_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!EstáSeleccionada)
                _solicitudSeleccionada();

            var leftButton = e.MouseDevice.LeftButton;
            if (leftButton == MouseButtonState.Pressed)
            {
                voiceEnablingTimer.Start();
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }

            var rightButton = e.MouseDevice.RightButton;
            if (rightButton == MouseButtonState.Pressed)
                _requestNumbersPopup(UI);
        }

        // Cuando el raton se mueve mientras tiene pulsado el botón izquierdo, detiene el timer de activación del modo Raton+Voz
        private Point lastPoint;
        private void UI_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var point = e.GetPosition((IInputElement)e.OriginalSource);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (voiceEnablingTimer.IsEnabled && point != lastPoint)
                    voiceEnablingTimer.Stop();
            }
            lastPoint = point;
        }

        // Cuando se levanta el botón izquierdo se para el timer de activación del modo Raton+Voz si no se había activado aún. 
        // En caso de que ya se hubiese activado, se desactiva el reconocimiento de voz.
        private void UI_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var leftButton = e.MouseDevice.LeftButton;
            if (leftButton != MouseButtonState.Pressed)
            { 
                if (voiceEnablingTimer.IsEnabled)
                {
                    voiceEnablingTimer.Stop();
                }
                else
                {
                    speechRecognitionService.SpeechRecognized -= SpeechRecognitionService_SpeechRecognized;
                    speechRecognitionService.RequestDisableRecognition();
                }
            }
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
        }

        #region public

        public void PonerNúmero(int número)
        {
            if (!_modificable) return;
            ForzarPonerNúmero(número);
        }

        public void QuitarNúmero()
        {
            if (!_modificable) return;
            _textBlock.Text = "";
            _textBlock.Visibility = Visibility.Hidden;
            _uniformGrid.Visibility = Visibility.Visible;
        }

        public void PonerPosible(int número)
        {
            if (!_modificable) return;
            (_uniformGrid.Children[número - 1] as TextBlock).Text = número.ToString();
        }

        public void QuitarPosible(int número)
        {
            if (!_modificable) return;
            (_uniformGrid.Children[número - 1] as TextBlock).Text = "";
        }

        public void QuitarTodosPosibles()
        {
            for (int número = 1; número <= Sudoku.Tamaño; ++número)
                QuitarPosible(número);
        }

        #endregion

        #region private

        readonly Action<int> _solicitudCambioNúmero;
        readonly Action _solicitudSeleccionada;
        readonly Action<UIElement> _requestNumbersPopup;
        static readonly FontFamily _fuente = new FontFamily("Comic Sans MS");
        bool _estáSeleccionado;
        readonly Border selecciónBorde = new Border() { BorderBrush = Brushes.Red, BorderThickness = new Thickness(2), Visibility = Visibility.Hidden };
        readonly UniformGrid _uniformGrid = new UniformGrid() { Rows = Sudoku.Tamaño / 3, Columns = Sudoku.Tamaño / 3 };
        readonly TextBlock _textBlock = new TextBlock()
        {
            Visibility = Visibility.Hidden,
            FontFamily = _fuente,
            FontSize = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        readonly bool _modificable;

        void ForzarPonerNúmero(int número)
        {
            _textBlock.Visibility = Visibility.Visible;
            _textBlock.Text = número.ToString();
            _uniformGrid.Visibility = Visibility.Hidden;
        }

        #endregion

        #region InkCanvas
        private const string acceptedCharRegex = @"^[1-9xX]{1}$";

        private InkCanvas inkCanvas = new InkCanvas
        {
            Background = Brushes.Transparent, // Transparente para que los numeros posibles puedan verse
            MinHeight = 0.0,
            MinWidth = 0.0,
            EditingMode = InkCanvasEditingMode.None
        };
        private DispatcherTimer timer;

        private void SetupInkCanvas()
        {
            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = TimeSpan.FromMilliseconds(1000);

            inkCanvas.MouseMove += InkCanvas_MouseMove;
        }

        // Se produce el reconocimento al dispararse el timer
        private void Timer_Tick(object sender, EventArgs e)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                inkCanvas.Strokes.Save(ms);
                var ink = new Ink();
                ink.Load(ms.ToArray());
                using (RecognizerContext context = new RecognizerContext())
                {
                    if (ink.Strokes.Count > 0)
                    {
                        context.Strokes = ink.Strokes;
                        RecognitionStatus status;
                        var result = context.Recognize(out status);

                        if (status == RecognitionStatus.NoError)
                        {
                            if (result == null)
                            {
                                ResetCanvasAndTimer();
                                return;
                            }

                            var resultString = result.TopString;

                            if (!isAcceptedChar(resultString)) 
                            {
                                ResetCanvasAndTimer();
                                return;
                            }

                            bool isNumeric = int.TryParse(resultString, out int number);

                            if (!isNumeric)
                            {
                                number = 0;
                            }

                            _solicitudCambioNúmero(number);
                            ResetCanvasAndTimer();
                        }
                    }
                }
            }
        }

        private void ResetCanvasAndTimer()
        {
            inkCanvas.Strokes.Clear();
            timer.Stop();
        }

        // Mientas el ratón se mueva por el inkcanvas con el botón izquierdo presionado se va reiniciando el timer
        private void InkCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                timer.Stop();
                timer.Start();
            }
        }

        // Se comprueba que sea un número valido (1-9) o una x para borrar
        private bool isAcceptedChar(string s)
        {
            return Regex.Match(s, acceptedCharRegex).Success;
        }

        #endregion

        #region Mouse&Voice
        private const string DELETE = "Borrar";

        private SpeechRecognitionService speechRecognitionService;
        private DispatcherTimer voiceEnablingTimer;

        private void SetupVoice()
        {
            // Se configura el timer que activa la entrada Raton+Voz
            // Si se mantiene el botón izquierdo del ratón pulsado sin moverse durante 500 ms se activa el reconocimiento de voz
            voiceEnablingTimer = new DispatcherTimer();
            voiceEnablingTimer.Interval = TimeSpan.FromMilliseconds(500);
            voiceEnablingTimer.Tick += VoiceEnablingTimer_Tick;

            speechRecognitionService = SpeechRecognitionService.GetInstance();
            
        }

        private void SpeechRecognitionService_SpeechRecognized(SpeechRecognizedEventArgs e)
        {
            string result = e.Result.Text;
            _solicitudCambioNúmero(result.Equals(DELETE) ? 0 : int.Parse(result));
        }

        // Al dispararse el timer, se desactiva el inkCanvas y se activa el reconocimiento de voz
        private void VoiceEnablingTimer_Tick(object sender, EventArgs e)
        {
            voiceEnablingTimer.Stop();
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            speechRecognitionService.SpeechRecognized += SpeechRecognitionService_SpeechRecognized;
            speechRecognitionService.RequestEnableRecognition();
        }

        #endregion

        // Se suscribe/dessuscribe a los eventos de ratón que activan los modos de entrada a nivel de celda.
        #region Enable/Disable input methods (VoiceOnly)
        public void EnableInputMethods()
        {
            UI.PreviewMouseDown += UI_MouseDown;
            UI.PreviewMouseMove += UI_PreviewMouseMove;
            UI.MouseUp += UI_MouseUp;
        }

        public void DisableInputMethods()
        {
            UI.PreviewMouseDown -= UI_MouseDown;
            UI.PreviewMouseMove -= UI_PreviewMouseMove;
            UI.MouseUp -= UI_MouseUp;
        }

        #endregion
    }
}

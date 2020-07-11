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
        public Celda(int número, Action<int> solicitudCambioNúmero, Action solicitudSeleccionada)
        {
            _solicitudCambioNúmero = solicitudCambioNúmero;
            _solicitudSeleccionada = solicitudSeleccionada;
            UI = new Border() { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5), Background=Brushes.Transparent };
            UI.PreviewMouseDown += new System.Windows.Input.MouseButtonEventHandler(UI_MouseDown);
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

            _modificable = número == 0;
            _textBlock.Foreground = _modificable ? Brushes.Blue : Brushes.Black;
            if (número != 0)
                ForzarPonerNúmero(número);
        }

        void UI_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _solicitudSeleccionada();
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
            Background = Brushes.Transparent,
            MinHeight = 0.0,
            MinWidth = 0.0
        };
        private DispatcherTimer timer;

        private void SetupInkCanvas()
        {
            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = TimeSpan.FromMilliseconds(1000);

            inkCanvas.MouseMove += InkCanvas_MouseMove;
        }

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

        private void InkCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                timer.Stop();
                timer.Start();
            }
        }

        private bool isAcceptedChar(string s)
        {
            return Regex.Match(s, acceptedCharRegex).Success;
        }

        #endregion
    }
}

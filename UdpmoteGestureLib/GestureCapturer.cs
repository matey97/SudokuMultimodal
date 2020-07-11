using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UdpmoteLib;

namespace UdpmoteGestureLib
{
    public class GestureCapturer
    {
        public event Action<Gesture> GestureCaptured;

        public void OnUdpmoteChanged(UdpmoteState state)
        {
            bool b = state.ButtonState.B;
            double[] sample = new double[]
            {
                state.AccelState.X,
                state.AccelState.Y,
                state.AccelState.Z
            };

            switch (captureState)
            {
                case CaptureState.Off:
                    if (b)
                    { 
                        gesture = new Gesture();
                        gesture.AddSample(sample);
                        captureState = CaptureState.On;
                    }
                    break;
                case CaptureState.On:
                    if (b)
                        gesture.AddSample(sample);
                    else
                    {
                        GestureCaptured?.Invoke(gesture);
                        captureState = CaptureState.Off;
                    }
                    break;
            }
        }

        #region Private
        private enum CaptureState { Off, On };

        private CaptureState captureState = CaptureState.Off;

        private Gesture gesture;
        #endregion
    }
}

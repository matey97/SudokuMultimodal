using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdpmoteGestureLib
{
    public class GestureRecognizer
    {
        public event Action<string> GestureRecognized;

        public List<Gesture> Prototypes { get; private set; }

        public GestureRecognizer()
        {
            Prototypes = new List<Gesture>();
        }

        public void AddPrototypes(Gesture prototype)
        {
            Prototypes.Add(prototype);
        }

        public void OnGestureCaptured(Gesture gesture)
        {
            if (Prototypes.Count == 0) return;

            var bestDistance = double.MaxValue;
            string bestPrototypeName = null;

            foreach (var prototype in Prototypes)
            {
                var distance = gesture.DistanceTo(prototype);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestPrototypeName = prototype.Name;
                }
            }

            if (GestureRecognized != null)
                GestureRecognized(bestPrototypeName);
        }
    }
}

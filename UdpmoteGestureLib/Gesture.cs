using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdpmoteGestureLib
{
    [Serializable]
    public class Gesture
    {
        readonly private List<double[]> samples = new List<double[]>();

        public string Name { get; set; }

        public int Count { get { return samples.Count; } }

        public Gesture()
        {
            Name = "<NoName>";
        }

        public IEnumerable<double[]> GetSamples()
        {
            return samples;
        }

        public void AddSample(double[] sample)
        {
            samples.Add(sample);
        }

        public double DistanceTo(Gesture other)
        {
            double[] current = new double[other.Count];
            double[] previous = new double[other.Count];
       
            current[0] = 2 * EuclideanDistance(samples[0], other.samples[0]);
            for (int j = 1; j < other.Count; j++)
                current[j] = current[j - 1] + EuclideanDistance(samples[0], other.samples[j]);
            for (int i = 1; i < samples.Count; i++)
            {
                { double[] aux = current; current = previous; previous = aux; }
                current[0] = previous[0] + EuclideanDistance(samples[i], other.samples[0]);
                for (int j = 1; j < other.samples.Count; j++)
                {
                    double d = EuclideanDistance(samples[i], other.samples[j]);
                    current[j] = Math.Min(Math.Min(current[j - 1], previous[j]), previous[j - 1] + d) + d;
                }
            }
            return current[other.samples.Count - 1] / (samples.Count + other.samples.Count);
        }

        private double EuclideanDistance(double[] p1, double[] p2)
        {
            var dx = p1[0] - p2[0];
            var dy = p1[1] - p2[1];
            var dz = p1[2] - p2[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}

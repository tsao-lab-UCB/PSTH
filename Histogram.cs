using OpenCV.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace PSTH
{
    public readonly struct UnitLabel : IComparable<UnitLabel>, IEquatable<UnitLabel>
    {
        public string Electrode { get; }
        public ushort SortedId { get; }

        public UnitLabel(string electrode, ushort sortedId)
        {
            Electrode = electrode;
            SortedId = sortedId;
        }

        public static bool operator ==(UnitLabel lhs, UnitLabel rhs) => lhs.Equals(rhs);

        public static bool operator !=(UnitLabel lhs, UnitLabel rhs) => !lhs.Equals(rhs);

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            var other = (UnitLabel) obj;
            return Equals(other);
        }

        public bool Equals(UnitLabel other) => Electrode == other.Electrode && SortedId == other.SortedId;

        public int CompareTo(UnitLabel other) =>
            StringComparer.InvariantCulture.Compare(ToString(), other.ToString());

        public override string ToString() => $"{Electrode}:{SortedId}";
        public override int GetHashCode() => HashCode.Combine(Electrode.GetHashCode(), SortedId.GetHashCode());
    }

    public class Histogram
    {
        public UnitLabel Unit { get; }
        public int ClassCount { get; private set; }
        public uint SpikeCount { get; private set; }
        public int BinCount => BinEdges.Length - 1;
        public double Min => BinEdges[0];
        public double Max => BinEdges[BinCount];
        public double BinWidth => BinEdges[1] - BinEdges[0];
        public double[] BinEdges { get; }
        public double[,] Data { get; private set; }
        public Mat Mat { get; private set; }

        public Histogram(UnitLabel unit, int classCount, double[] binEdges)
            : this(unit, (classCount == 0 || binEdges == null) ? null : new double[classCount, binEdges.Length - 1],
                binEdges)
        {
        }

        private Histogram(UnitLabel unit, double[,] data, double[] binEdges)
        {
            if (data == null || binEdges == null)
                throw new ArgumentNullException();
            var binCount = data.GetLength(1);
            if (binEdges.Length != binCount + 1)
                throw new ArgumentException();
            ClassCount = data.GetLength(0);
            Unit = unit;
            Data = data;
            Mat = Mat.CreateMatHeader(Data);
            BinEdges = binEdges;
        }

        public void AddClass(int classId)
        {
            var dataNew = new double[++ClassCount, BinCount];
            if (classId > 0)
                Array.Copy(Data, dataNew, classId * BinCount);
            if (classId < ClassCount - 1)
                Array.Copy(Data, classId * BinCount, dataNew,
                    (classId + 1) * BinCount, (ClassCount - classId - 1) * BinCount);
            Data = dataNew;
            Mat = Mat.CreateMatHeader(Data);
        }

        public void AddSample(double t, int classId)
        {
            var binId = (int) Math.Floor((t - Min) / BinWidth);
            if (binId < 0 || binId >= BinCount) return;
            Data[classId, binId] += 1;
            SpikeCount += 1;
        }

        public Histogram Clone()
        {
            return new Histogram(Unit, (double[,]) Data.Clone(), BinEdges) {SpikeCount = SpikeCount};
        }

        public Histogram Output(IReadOnlyList<uint> counts, double[] kernel)
        {
            if (counts == null || counts.Count != ClassCount)
                throw new ArgumentException();

            var h = Clone();
            var halfKernelLength = kernel?.Length / 2 ?? 0;

            if (halfKernelLength == 0)
            {
                for (var i = 0; i < ClassCount; i++)
                {
                    var factor = 1000f / BinWidth / counts[i];
                    for (var j = 0; j < BinCount; j++)
                    {
                        h.Data[i, j] *= factor;
                    }
                }
            }
            else
            {
                var result = new double[ClassCount, BinCount];
                for (var i = 0; i < ClassCount; i++)
                {
                    var factor = 1000f / BinWidth / counts[i];
                    for (var j = 0; j < BinCount; j++)
                    {
                        var sum = 0.0;
                        for (var k = 0; k < kernel.Length; k++)
                        {
                            var index = j + k - (kernel.Length - 1) / 2;
                            if (index >= 0 && index < BinCount)
                                sum += h.Data[i, index] * kernel[k];
                        }

                        result[i, j] = sum * factor;
                    }
                }

                Buffer.BlockCopy(result, 0, h.Data, 0, ClassCount * BinCount * 8);
            }

            return h;
        }

        public override string ToString()
        {
            return $"[Unit {Unit}, {SpikeCount} spikes]";
        }
    }

    public class HistogramList<TClass> : IReadOnlyList<Histogram>
    {
        public IEnumerable<UnitLabel> Units => _units;
        public IEnumerable<TClass> Classes => _classes;
        public IEnumerable<uint> Counts => _counts;
        //public IObservable<Mat> Mats => _histograms.Select(h => h.Mat).ToObservable();
        public int BinCount => BinEdges.Length - 1;
        public int ClassCount => _classes.Count;
        public int Count => _units.Count;
        public double Min => BinEdges[0];
        public double Max => BinEdges[BinCount];
        public double BinWidth => BinEdges[1] - BinEdges[0];
        public double[] BinEdges { get; private set; }
        public SpikeHistogram Source { get; }

        private readonly SortedArray<UnitLabel> _units = new SortedArray<UnitLabel>(8);
        private readonly SortedArray<TClass> _classes = new SortedArray<TClass>(8);
        private readonly List<uint> _counts = new List<uint>(8);
        private readonly List<Histogram> _histograms = new List<Histogram>(8);
        private readonly object _gate = new object();

        public HistogramList(SpikeHistogram source = null)
        {
            Source = source;
        }

        protected HistogramList(SortedArray<UnitLabel> units, SortedArray<TClass> classes,
            List<uint> counts, List<Histogram> histograms, double[] binEdges, SpikeHistogram source = null)
        {
            _units = units;
            _classes = classes;
            _counts = counts;
            _histograms = histograms;
            BinEdges = binEdges;
            Source = source;
        }

        public void Reset()
        {
            lock (_gate)
            {
                _units.Clear();
                _classes.Clear();
                _counts.Clear();
                _histograms.Clear();
            }
        }

        public void AddSamples(Triggered<Timestamped<OpenEphysData>[], TClass> samples,
            int binCount, double leftEdgeMs, double rightEdgeMs)
        {
            lock (_gate)
            {
                var binWidth = (rightEdgeMs - leftEdgeMs) / binCount;

                if (BinEdges == null || BinEdges.Length == 0 || binCount != BinCount || leftEdgeMs != Min)
                {
                    Reset();
                    BinEdges = new double[binCount + 1];
                    for (var i = 0; i < binCount + 1; i++)
                    {
                        BinEdges[i] = leftEdgeMs + i * binWidth;
                    }
                }

                if (_classes.TryAdd(samples.Class, out var classId))
                {
                    foreach (var hist in _histograms)
                    {
                        hist.AddClass(classId);
                    }

                    _counts.Insert(classId, 0);
                }

                foreach (var d in samples.Value)
                {
                    if (d.Value.Type != DataType.Spike) continue;
                    var unit = new UnitLabel(d.Value.Electrode, d.Value.SortedId);
                    if (_units.TryAdd(unit, out var unitId))
                    {
                        _histograms.Insert(unitId, new Histogram(
                            unit, _classes.Count, BinEdges));
                    }

                    _histograms[unitId].AddSample((double) (d.Timestamp - samples.Timestamp).TotalMilliseconds,
                        classId);
                }

                _counts[classId]++;
            }
        }

        public HistogramList Output(double[] kernel)
        {
            lock (_gate)
            {
                if (_units.Count == 0) return new HistogramList();
                var histograms = _histograms.Select(h => h.Output(_counts, kernel)).ToList();
                return new HistogramList(_units.Clone(), _classes.Convert(),
                    new List<uint>(_counts), histograms, (double[])BinEdges.Clone(), typeof(TClass), Source);
            }
        }

        public Histogram this[int index] 
        {
            get
            {
                lock (_gate)
                {
                    return _histograms[index];
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Histogram> GetEnumerator()
        {
            return ((IEnumerable<Histogram>) _histograms).GetEnumerator();
        }

        public override string ToString()
        {
            return $"Histogram: classes [{string.Join(", ", _classes)}]\n\t" +
                   string.Join("\n\t", _histograms);
        }
    }

    public sealed class HistogramList : HistogramList<object>
    {
        public Type Type { get; }

        internal HistogramList()
        {
        }

        internal HistogramList(SortedArray<UnitLabel> units, SortedArray<object> classes, List<uint> counts,
            List<Histogram> histograms, double[] binEdges, Type type, SpikeHistogram source = null)
            : base(units, classes, counts, histograms, binEdges, source)
        {
            Type = type;
        }
    }
}

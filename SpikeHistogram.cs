using Bonsai;
using Bonsai.Dsp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;
using OpenCV.Net;
using Buffer = System.Buffer;

namespace PSTH
{
    public class Histogram<TClass>
    {
        public string Unit { get; }
        public int ClassCount { get; private set; }
        public uint SpikeCount { get; private set; }
        public int BinCount => BinEdges.Length - 1;
        public float Min => BinEdges[0];
        public float Max => BinEdges[BinCount];
        public float BinWidth => BinEdges[1] - BinEdges[0];
        public float[] BinEdges { get; }
        public float[,] Data { get; private set; }
        public Mat Mat { get; private set; }

        public Histogram(string unit, int classCount, float[] binEdges)
            : this(unit, (classCount == 0 || binEdges == null) ? null : new float[classCount, binEdges.Length - 1],
                binEdges)
        {
        }

        private Histogram(string unit, float[,] data, float[] binEdges)
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
            var dataNew = new float[++ClassCount, BinCount];
            if (classId > 0)
                Array.Copy(Data, dataNew, classId * BinCount);
            if (classId < ClassCount - 1)
                Array.Copy(Data, classId * BinCount, dataNew,
                    (ClassCount - 1) * BinCount, (ClassCount - classId - 1) * BinCount);
            Data = dataNew;
            Mat = Mat.CreateMatHeader(Data);
        }

        public void AddSample(float t, int classId)
        {
            var binId = (int)Math.Floor((t - Min) / BinWidth);
            if (binId < 0 || binId >= BinCount) return;
            Data[classId, binId] += 1;
            SpikeCount += 1;
        }

        public Histogram<TClass> Clone()
        {
            return new Histogram<TClass>(Unit, (float[,]) Data.Clone(), BinEdges) {SpikeCount = SpikeCount};
        }

        public Histogram<TClass> Output(IReadOnlyList<uint> counts, float[] kernel)
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
                var result = new float[ClassCount, BinCount];
                for (var i = 0; i < ClassCount; i++)
                {
                    var factor = 1000f / BinWidth / counts[i];
                    for (var j = 0; j < BinCount; j++)
                    {
                        var sum = 0f;
                        for (var k = 0; k < kernel.Length; k++)
                        {
                            var index = j + k - (kernel.Length - 1) / 2;
                            if (index >= 0 && index < BinCount)
                                sum += h.Data[i, index] * kernel[k];
                        }
                        result[i, j] = sum * factor;
                    }
                }

                Buffer.BlockCopy(result, 0, h.Data, 0, ClassCount * BinCount * 4);
            }

            return h;
        }

        public override string ToString()
        {
            return $"[Unit {Unit}, {SpikeCount} spikes]";
        }
    }

    public class HistogramCollection<TClass>
    {
        public string[] Units => _units.ToArray();
        public TClass[] Classes => _classes.ToArray();
        public uint[] Counts => _counts.ToArray();
        public Histogram<TClass>[] Histograms => _histograms.ToArray();
        public IObservable<Mat> Mats => _histograms.Select(h => h.Mat).ToObservable();
        public int BinCount => BinEdges.Length - 1;
        public float Min => BinEdges[0];
        public float Max => BinEdges[BinCount];
        public float BinWidth => BinEdges[1] - BinEdges[0];
        public float[] BinEdges { get; private set; }

        private readonly SortedArray<string> _units = new SortedArray<string>(8);
        private readonly SortedArray<TClass> _classes = new SortedArray<TClass>(8);
        private readonly List<uint> _counts = new List<uint>(8);
        private readonly List<Histogram<TClass>> _histograms = new List<Histogram<TClass>>(8);

        public HistogramCollection()
        {
        }

        private HistogramCollection(SortedArray<string> units, SortedArray<TClass> classes,
            List<uint> counts, List<Histogram<TClass>> histograms, float[] binEdges)
        {
            _units = units;
            _classes = classes;
            _counts = counts;
            _histograms = histograms;
            BinEdges = binEdges;
        }

        public void Reset()
        {
            _units.Clear();
            _classes.Clear();
            _counts.Clear();
            _histograms.Clear();
        }

        public void AddSamples(Triggered<Timestamped<OpenEphysData>[], TClass> samples, 
            int binCount, float leftEdgeMs, float rightEdgeMs)
        {
            try
            {
                var binWidth = (rightEdgeMs - leftEdgeMs) / binCount;

                if (BinEdges == null || BinEdges.Length == 0 || binCount != BinCount || leftEdgeMs != Min)
                {
                    Reset();
                    BinEdges = new float[binCount + 1];
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
                    var unit = $"{d.Value.Electrode}:{d.Value.SortedId}";
                    if (_units.TryAdd(unit, out var unitId))
                    {
                        _histograms.Insert(unitId, new Histogram<TClass>(
                            unit, _classes.Count, BinEdges));
                    }
                    _histograms[unitId].AddSample((float)(d.Timestamp - samples.Timestamp).TotalMilliseconds, classId);
                }

                _counts[classId]++;
            }
            catch (Exception e)
            {
                Console.Out.WriteLine($"Exception {e} caught when adding samples. Resetting");
                Reset();
            }
        }

        public HistogramCollection<TClass> Output(float[] kernel)
        {
            if (_units.Count == 0) return new HistogramCollection<TClass>();
            var histograms = _histograms.Select(h => h.Output(_counts, kernel)).ToList();
             return new HistogramCollection<TClass>(_units.Clone(), _classes.Clone(),
                new List<uint>(_counts), histograms, (float[]) BinEdges.Clone());
        }

        public override string ToString()
        {
            return $"Histogram: classes [{string.Join(", ", _classes)}]\n\t" +
                   string.Join<Histogram<TClass>>("\n\t", Histograms);
        }
    }

    /// <summary>
    /// Represents an operator that calculates PSTH for spikes triggered on signals of different classes.
    /// </summary>
    [Combinator]
    [Description("SpikeHistogram")]
    [WorkflowElementCategory(ElementCategory.Combinator)]
    public class SpikeHistogram
    {
        /// <summary>
        /// The width of the negative half window of the histogram in ms.
        /// </summary>
        [Description("The width of the negative half window of the histogram in ms.")]
        public float LeftHalfWindowMs
        {
            get => _leftHalfWindowMs;
            set
            {
                if (Math.Abs(value - _leftHalfWindowMs) < 0.001f || value < 0) return;
                _leftHalfWindowMs = value;
                _binWidthMs = WindowWidthMs / _binCount;
                Reset();
            }
        }

        /// <summary>
        /// The width of the positive half window of the histogram in ms.
        /// </summary>
        [Description("The width of the positive half window of the histogram in ms.")]
        public float RightHalfWindowMs
        {
            get => _rightHalfWindowMs;
            set
            {
                if (Math.Abs(value - _rightHalfWindowMs) < 0.001f || value < 0) return;
                _rightHalfWindowMs = value;
                _binWidthMs = WindowWidthMs / _binCount;
                Reset();
            }
        }

        /// <summary>
        /// The number of bins in the histogram.
        /// </summary>
        [Description("The number of bins in the histogram.")]
        public int BinCount
        {
            get => _binCount;
            set
            {
                if (value == _binCount || value <= 0) return;
                _binCount = value;
                _binWidthMs = WindowWidthMs / _binCount;
                Reset();
            }
        }

        /// <summary>
        /// The width of bins in ms.
        /// </summary>
        [Description("The width of bins in ms.")]
        public float BinWidthMs
        {
            get => _binWidthMs;
            set
            {
                if (value <= 0) return;
                _binCount = (int) Math.Round(WindowWidthMs / value);
                _binWidthMs = WindowWidthMs / _binCount;
                Reset();
            }
        }

        /// <summary>
        /// The sigma of the Gaussian smoothing kernel in ms. 0 means no filtering.
        /// </summary>
        [Description("The sigma of the Gaussian smoothing kernel in ms. 0 means no filtering.")]
        public float FilterSigmaMs
        {
            get => _filterSigmaMs;
            set
            {
                if (value < 0) return;
                _filterSigmaMs = value;
                Reset();
            }
        }

        [Browsable(false)]
        public float WindowWidthMs => _rightHalfWindowMs + _leftHalfWindowMs;

        private float _leftHalfWindowMs, _rightHalfWindowMs = 1000f, _filterSigmaMs, _binWidthMs = 1f;
        private int _binCount = 1000;
        private float[] _kernel;
        //private Mat _mat;
        private readonly Subject<Unit> _resetSubject = new Subject<Unit>();
        //private readonly Subject<Mat> _matSubject = new Subject<Mat>();
        private readonly WindowBackTrigger _windowBackTrigger = new WindowBackTrigger();
        //private readonly FirFilter _filter = new FirFilter();
        //private readonly IDisposable _filterSub;

        //public SpikeHistogram()
        //{
        //    _filterSub = _filter.Process(_matSubject).Subscribe(mat => _mat = mat);
        //}

        //private Mat Filter(Mat mat)
        //{
        //    if (_filterSigmaMs < 0.001) return mat;
        //    _matSubject.OnNext(mat);
        //    return _mat.Clone();
        //}

        private void Reset()
        {
            _windowBackTrigger.LeftHalfWindow = TimeSpan.FromMilliseconds(_leftHalfWindowMs);
            _windowBackTrigger.RightHalfWindow = TimeSpan.FromMilliseconds(_rightHalfWindowMs);
            var kernelHalfLength = (int)Math.Ceiling(_filterSigmaMs * 4 / _binWidthMs);
            var kernelLength = kernelHalfLength * 2 + 1;
            _kernel = new float[kernelLength];
            var sum = 0f;
            var q = 2 * _filterSigmaMs * _filterSigmaMs / _binWidthMs / _binWidthMs;
            
            for (var i = 0; i < kernelLength; i++)
            {
                var j = i - kernelHalfLength;
                _kernel[i] = (float)Math.Exp(-j * j / q);
                sum += _kernel[i];
            }
            
            for (var i = 0; i < kernelLength; i++)
            {
                _kernel[i] /= sum;
            }

            //_mat = null;
            //_filter.Kernel = _kernel;
            //_filter.Anchor = kernelHalfLength;
            _resetSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// Calculates PSTH for spikes triggered on signals of different classes.
        /// </summary>
        /// <typeparam name="TClass">Type of the classes of triggers, should be sortable.</typeparam>
        /// <typeparam name="TReset">Type of the reset signal to clear the histograms</typeparam>
        /// <param name="source">The source sequence from OpenEphysParser with spikes only.</param>
        /// <param name="trigger">The trigger sequence.</param>
        /// <param name="reset">The trigger sequence of any type.</param>
        /// <returns></returns>
        public IObservable<HistogramCollection<TClass>> Process<TClass, TReset>(
            IObservable<Timestamped<OpenEphysData>> source, IObservable<Timestamped<TClass>> trigger, 
            IObservable<TReset> reset)
        {
            Reset();
            var histograms = new HistogramCollection<TClass>();
            var triggered = _windowBackTrigger.Process(source, trigger);
            return Observable.Create<HistogramCollection<TClass>>(observer =>
            {
                var resetSub = reset.Select(_ => Unit.Default).Merge(_resetSubject).Subscribe(_ =>
                {
                    histograms.Reset();
                    observer.OnNext(histograms.Output(_kernel));
                });
                var sourceSub = triggered.Subscribe(samples =>
                {
                    histograms.AddSamples(samples, _binCount, -_leftHalfWindowMs, _rightHalfWindowMs);
                    observer.OnNext(histograms.Output(_kernel));
                });
                return Disposable.Create(() =>
                {
                    resetSub.Dispose();
                    sourceSub.Dispose();
                });
            });
        }

        //public IObservable<HistogramCollection<TClass>> Process<TClass, TReset>(
        //    IObservable<Triggered<Timestamped<OpenEphysData>[], TClass>> source, IObservable<TReset> reset)
        //{
        //    var histograms = new HistogramCollection<TClass>();
        //    _resetSubject.Subscribe(_ => histograms.Reset());
        //    return Observable.Create<HistogramCollection<TClass>>(observer =>
        //    {
        //        var resetSub = reset.Subscribe(_ =>
        //        {
        //            histograms.Reset();
        //            observer.OnNext(histograms);
        //        });
        //        var sourceSub = source.Subscribe(samples =>
        //        {
        //            histograms.AddSamples(samples, _binCount, _leftHalfWindow, _rightHalfWindow);
        //            observer.OnNext(histograms);
        //        });
        //        return Disposable.Create(() =>
        //        {
        //            resetSub.Dispose();
        //            sourceSub.Dispose();
        //        });
        //    });
        //}

        //~SpikeHistogram()
        //{
        //    _filterSub.Dispose();
        //}
    }
}

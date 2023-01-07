using Bonsai;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;
using OpenCV.Net;

namespace PSTH
{
    public class Histogram<TClass>
    {
        public int ClassCount { get; private set; }
        public int BinCount => BinEdges.Length - 1;

        public float Min => BinEdges[0];
        public float Max => BinEdges[BinCount];
        public float BinWidth => BinEdges[1] - BinEdges[0];
        public string Unit { get; }
        public float[] BinEdges { get; }
        public uint SpikeCount { get; private set; } = 0;
        public float[,] Data { get; private set; }
        public Mat Mat { get; private set; }

        public static bool AddIfDistinct<T>(List<T> list, T item, out int index)
        {
            index = list.IndexOf(item);
            if (index >= 0)
                return false;
            index = list.Count;
            list.Add(item);
            return true;
        }

        public Histogram(string unit, int classCount, int binCount, float leftEdge, float rightEdge)
            : this(unit, classCount == 0 ? null : new float[classCount, binCount], leftEdge, rightEdge)
        {
        }

        private Histogram(string unit, float[,] data, float leftEdge, float rightEdge)
        {
            if (data == null || leftEdge >= rightEdge)
                throw new ArgumentNullException();
            var binCount = data.GetLength(1);
            ClassCount = data.GetLength(0);
            Unit = unit;
            Data = data;
            Mat = Mat.CreateMatHeader(Data);
            BinEdges = new float[binCount + 1];
            var binWidth = (rightEdge - leftEdge) / binCount;
            for (var i = 0; i < binCount + 1; i++)
            {
                BinEdges[i] = leftEdge + i * binWidth;
            }
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
            return new Histogram<TClass>(Unit, (float[,]) Data.Clone(), Min, Max);
        }

        public Histogram<TClass> Normalized(IReadOnlyList<uint> counts)
        {
            if (counts == null || counts.Count != ClassCount)
                throw new ArgumentException();
            var h = Clone();
            for (var i = 0; i < ClassCount; i++)
            {
                var factor = 1000f / BinWidth / counts[i];
                for (var j = 0; j < BinCount; j++)
                {
                    h.Data[i, j] *= factor;
                }
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
        //public IObservable<float[,]> Data => Histograms.Select(h => h.Data).ToObservable();

        private readonly SortedArray<string> _units = new SortedArray<string>(8);
        private readonly SortedArray<TClass> _classes = new SortedArray<TClass>(8);
        private readonly List<uint> _counts = new List<uint>(8);
        private readonly List<Histogram<TClass>> _histograms = new List<Histogram<TClass>>(8);

        public HistogramCollection()
        {
        }

        private HistogramCollection(SortedArray<string> units, SortedArray<TClass> classes,
            List<uint> counts, List<Histogram<TClass>> histograms)
        {
            _units = units;
            _classes = classes;
            _counts = counts;
            _histograms = histograms;
        }

        public void Reset()
        {
            _units.Clear();
            _classes.Clear();
            _counts.Clear();
            _histograms.Clear();
        }

        public void AddSamples(Triggered<Timestamped<OpenEphysData>[], TClass> samples, 
            int binCount, TimeSpan leftHalfWindow, TimeSpan rightHalfWindow)
        {
            try
            {
                var leftEdge = (float)-leftHalfWindow.TotalMilliseconds;
                var rightEdge = (float)rightHalfWindow.TotalMilliseconds;
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
                            unit, _classes.Count, binCount, leftEdge, rightEdge));
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

        public HistogramCollection<TClass> Output()
        {
            var histograms = _histograms.Select(h => h.Normalized(_counts)).ToList();
            return new HistogramCollection<TClass>(_units, _classes, _counts, histograms);
        }

        public override string ToString()
        {
            return $"Histogram: classes [{string.Join(", ", _classes)}]\n\t" +
                   string.Join<Histogram<TClass>>("\n\t", Histograms);
        }
    }

    [Combinator]
    [Description("SpikeHistogram")]
    [WorkflowElementCategory(ElementCategory.Combinator)]
    public class SpikeHistogram
    {
        [XmlIgnore]
        [Description("The width of the negative half window of the histogram")]
        public TimeSpan LeftHalfWindow
        {
            get => _leftHalfWindow;
            set
            {
                if (value == _leftHalfWindow || value < TimeSpan.Zero) return;
                _leftHalfWindow = value;
                _binWidthMs = WindowWidthMs / _binCount;
                Reset();
            }
        }

        [Browsable(false)]
        [XmlElement(nameof(LeftHalfWindow))]
        public string LeftHalfWindowXml
        {
            get => XmlConvert.ToString(LeftHalfWindow);
            set => LeftHalfWindow = !string.IsNullOrEmpty(value) ? XmlConvert.ToTimeSpan(value) : default;
        }

        [XmlIgnore]
        [Description("The width of the positive half window of the histogram")]
        public TimeSpan RightHalfWindow
        {
            get => _rightHalfWindow;
            set
            {
                if (value == _rightHalfWindow || value < TimeSpan.Zero) return;
                _rightHalfWindow = value;
                _binWidthMs = WindowWidthMs / _binCount;
                Reset();
            }
        }

        [Browsable(false)]
        [XmlElement(nameof(RightHalfWindow))]
        public string RightHalfWindowXml
        {
            get => XmlConvert.ToString(RightHalfWindow);
            set => RightHalfWindow = !string.IsNullOrEmpty(value) ? XmlConvert.ToTimeSpan(value) : default;
        }

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

        [Description("The width of bins in ms.")]
        public double BinWidthMs
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

        public double WindowWidthMs => (_rightHalfWindow + _leftHalfWindow).TotalMilliseconds;

        private TimeSpan _leftHalfWindow = TimeSpan.Zero, _rightHalfWindow = TimeSpan.FromSeconds(1);
        private int _binCount = 1;
        private double _binWidthMs = 1000;
        private readonly Subject<Unit> _resetSubject = new Subject<Unit>();
        private readonly WindowBackTrigger _windowBackTrigger = new WindowBackTrigger();

        private void Reset()
        {
            _windowBackTrigger.LeftHalfWindow = _leftHalfWindow;
            _windowBackTrigger.RightHalfWindow = _rightHalfWindow;
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
            var histograms = new HistogramCollection<TClass>();
            _windowBackTrigger.LeftHalfWindow = _leftHalfWindow;
            _windowBackTrigger.RightHalfWindow = _rightHalfWindow;
            _resetSubject.Subscribe(_ => histograms.Reset());
            var triggered = _windowBackTrigger.Process(source, trigger);
            return Observable.Create<HistogramCollection<TClass>>(observer =>
            {
                var resetSub = reset.Subscribe(_ =>
                {
                    histograms.Reset();
                    observer.OnNext(histograms.Output());
                });
                var sourceSub = triggered.Subscribe(samples =>
                {
                    histograms.AddSamples(samples, _binCount, _leftHalfWindow, _rightHalfWindow);
                    observer.OnNext(histograms.Output());
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
    }
}

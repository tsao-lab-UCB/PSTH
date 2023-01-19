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
        public double LeftHalfWindowMs
        {
            get => _leftHalfWindowMs;
            set
            {
                if (Math.Abs(value - _leftHalfWindowMs) < 0.001 || value < 0) return;
                _leftHalfWindowMs = value;
                _binCount = (int) Math.Round(WindowWidthMs / _binWidthMs);
                _binWidthMs = WindowWidthMs / _binCount;
                ResetParameters();
            }
        }

        /// <summary>
        /// The width of the positive half window of the histogram in ms.
        /// </summary>
        [Description("The width of the positive half window of the histogram in ms.")]
        public double RightHalfWindowMs
        {
            get => _rightHalfWindowMs;
            set
            {
                if (Math.Abs(value - _rightHalfWindowMs) < 0.001 || value < 0) return;
                _rightHalfWindowMs = value;
                _binCount = (int)Math.Round(WindowWidthMs / _binWidthMs);
                _binWidthMs = WindowWidthMs / _binCount;
                ResetParameters();
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
                ResetParameters();
            }
        }

        /// <summary>
        /// The width of bins in ms.
        /// </summary>
        [Description("The width of bins in ms.")]
        public double BinWidthMs
        {
            get => _binWidthMs;
            set
            {
                if (value <= 0) return;
                _binCount = (int) Math.Round(WindowWidthMs / value);
                _binWidthMs = WindowWidthMs / _binCount;
                ResetParameters();
            }
        }

        /// <summary>
        /// The sigma of the Gaussian smoothing kernel in ms. 0 means no filtering.
        /// </summary>
        [Description("The sigma of the Gaussian smoothing kernel in ms. 0 means no filtering.")]
        public double FilterSigmaMs
        {
            get => _filterSigmaMs;
            set
            {
                if (value < 0) return;
                _filterSigmaMs = value;
                ResetParameters();
            }
        }

        private double LeftHalfBufferMs => _leftHalfWindowMs + _filterSigmaMs * 3;
        private double RightHalfBufferMs => _rightHalfWindowMs + _filterSigmaMs * 3;

        [Browsable(false)]
        public double WindowWidthMs => _rightHalfWindowMs + _leftHalfWindowMs;

        private double _leftHalfWindowMs, _rightHalfWindowMs = 1000, _filterSigmaMs, _binWidthMs = 1;
        private int _binCount = 1000;
        private double[] _kernel;
        private readonly Subject<Unit> _resetSubject = new Subject<Unit>();
        private readonly WindowBackTrigger _windowBackTrigger = new WindowBackTrigger();

        private void ResetParameters()
        {
            _windowBackTrigger.LeftHalfWindow = TimeSpan.FromMilliseconds(LeftHalfBufferMs);
            _windowBackTrigger.RightHalfWindow = TimeSpan.FromMilliseconds(RightHalfBufferMs);
            var kernelHalfLength = (int)Math.Ceiling(_filterSigmaMs * 4 / _binWidthMs);
            var kernelLength = kernelHalfLength * 2 + 1;
            _kernel = new double[kernelLength];
            var sum = 0.0;
            var q = 2 * _filterSigmaMs * _filterSigmaMs / _binWidthMs / _binWidthMs;
            
            for (var i = 0; i < kernelLength; i++)
            {
                var j = i - kernelHalfLength;
                _kernel[i] = Math.Exp(-j * j / q);
                sum += _kernel[i];
            }
            
            for (var i = 0; i < kernelLength; i++)
            {
                _kernel[i] /= sum;
            }

            Reset();
        }

        public void Reset()
        {
            _resetSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// Calculates PSTH for spikes triggered on signals of different classes.
        /// </summary>
        /// <typeparam name="TClass">Type of the classes of triggers, should be sortable.</typeparam>
        /// <param name="source">The source sequence from OpenEphysParser with spikes only.</param>
        /// <param name="trigger">The trigger sequence.</param>
        /// <returns></returns>
        public IObservable<HistogramList> Process<TClass>(
            IObservable<Timestamped<OpenEphysData>> source, IObservable<Timestamped<TClass>> trigger)
        {
            return Process<TClass, object>(source, trigger, null);
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
        public IObservable<HistogramList> Process<TClass, TReset>(
            IObservable<Timestamped<OpenEphysData>> source, IObservable<Timestamped<TClass>> trigger, 
            IObservable<TReset> reset)
        {
            ResetParameters();
            var histograms = new HistogramList<TClass>(this);
            var triggered = _windowBackTrigger.Process(source, trigger);
            return Observable.Create<HistogramList>(observer =>
            {
                IDisposable resetSub;
                if (reset != null)
                    resetSub = reset.Select(_ => Unit.Default).Merge(_resetSubject).Subscribe(_ =>
                    {
                        histograms.Reset();
                        observer.OnNext(histograms.Output(_kernel));
                    });
                else
                    resetSub = _resetSubject.Subscribe(_ =>
                    {
                        histograms.Reset();
                        observer.OnNext(histograms.Output(_kernel));
                    });

                var sourceSub = triggered.Subscribe(samples =>
                {
                    histograms.AddSamples(samples, _binCount, -LeftHalfBufferMs, RightHalfBufferMs);
                    observer.OnNext(histograms.Output(_kernel));
                });
                return Disposable.Create(() =>
                {
                    resetSub?.Dispose();
                    sourceSub.Dispose();
                });
            });
        }
    }
}

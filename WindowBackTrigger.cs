using Bonsai;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace PSTH
{
    [Serializable]
    public struct Triggered<T, TClass> : IEquatable<Triggered<T, TClass>>
    {
        public T Value { get; }
        public TClass Class { get; }
        public DateTimeOffset Timestamp { get; }

        public Triggered(T value, TClass @class, DateTimeOffset timestamp)
        {
            Timestamp = timestamp;
            Value = value;
            Class = @class;
        }

        public bool Equals(Triggered<T, TClass> other) =>
            other.Timestamp.Equals(Timestamp) &&
            Value.Equals(other.Value) &&
            Class.Equals(other.Class);

        public static bool operator ==(Triggered<T, TClass> first, Triggered<T, TClass> second) => first.Equals(second);

        public static bool operator !=(Triggered<T, TClass> first, Triggered<T, TClass> second) => !first.Equals(second);

        public override bool Equals(object obj) => obj is Triggered<T, TClass> other && Equals(other);

        public override int GetHashCode()
        {
            var num1 = Value == null ? 1979 : Value.GetHashCode();
            var num2 = Class == null ? 2729 : Class.GetHashCode();
            return (Timestamp.GetHashCode() ^ num1 + 3791) ^ num2;
        }

        public override string ToString() =>
            string.Format(CultureInfo.CurrentCulture, "{0}:{1}@{2}", Value, Class, Timestamp);
    }

    /// <summary>
    /// Represents an operator that create windows of samples from the first sequence within a certain amount of time
    /// in the past when the second sequence emits a notification.
    /// </summary>
    [Combinator]
    [Description("Create windows of samples from the first sequence within a certain amount of time in the past (and future) " +
                 "when the second sequence emits a notification.")]
    [WorkflowElementCategory(ElementCategory.Combinator)]
    public class WindowBackTrigger
    {
        [XmlIgnore]
        [Description("The time length of the buffer before the trigger arrives")]
        public TimeSpan LeftHalfWindow { get; set; } = TimeSpan.Zero;

        [Browsable(false)]
        [XmlElement(nameof(LeftHalfWindow))]
        public string LeftHalfWindowXml
        {
            get => XmlConvert.ToString(LeftHalfWindow);
            set => LeftHalfWindow = !string.IsNullOrEmpty(value) ? XmlConvert.ToTimeSpan(value) : default;
        }

        [XmlIgnore]
        [Description("The time length of the buffer after the trigger arrives")]
        public TimeSpan RightHalfWindow { get; set; } = TimeSpan.FromSeconds(1);

        [Browsable(false)]
        [XmlElement(nameof(RightHalfWindow))]
        public string RightHalfWindowXml
        {
            get => XmlConvert.ToString(RightHalfWindow);
            set => RightHalfWindow = !string.IsNullOrEmpty(value) ? XmlConvert.ToTimeSpan(value) : default;
        }

        private void UpdateQueue<T>(Queue<Timestamped<T>> queue, DateTimeOffset now, TimeSpan tolerance = default)
        {
            var last = now - LeftHalfWindow - RightHalfWindow - tolerance;
            while (queue.Count > 0 && queue.Peek().Timestamp < last)
                queue.Dequeue();
        }

        /// <summary>
        /// Create windows of samples from the first sequence within a certain amount of time in the past
        /// when the second sequence emits a notification.
        /// </summary>
        /// <typeparam name="TSource">
        /// The type of the timestamped elements in the <paramref name="source"/> sequence.
        /// </typeparam>
        /// <typeparam name="TClass">
        /// The type of the timestamped elements in the <paramref name="trigger"/> sequence.
        /// </typeparam>
        /// <param name="source">The source sequence to produce windows over.</param>
        /// <param name="trigger">The sequence of triggers. </param>
        /// <returns></returns>
        public IObservable<Triggered<Timestamped<TSource>[], TClass>> Process<TSource, TClass>(
            IObservable<Timestamped<TSource>> source, IObservable<Timestamped<TClass>> trigger)
        {
            var queue = new Queue<Timestamped<TSource>>(64);
            var tolerance = TimeSpan.FromMilliseconds(2000);
            return Observable.Create<Triggered<Timestamped<TSource>[], TClass>>(observer =>
            {
                var sourceSub = source.Subscribe(v =>
                {
                    queue.Enqueue(v);
                    UpdateQueue(queue, v.Timestamp, tolerance);
                });
                var delayedTrigger = trigger.Delay(RightHalfWindow);
                var triggerSub = trigger
                    .Zip(delayedTrigger, (v1, v2) => (v1, v2))
                    .Subscribe(v =>
                    {
                        var (v1, v2) = v;
                        UpdateQueue(queue, v2.Timestamp);
                        observer.OnNext(
                            new Triggered<Timestamped<TSource>[], TClass>(queue.ToArray(), v2.Value,
                                v1.Timestamp));
                    });
                return Disposable.Create(() =>
                {
                    sourceSub.Dispose();
                    triggerSub.Dispose();
                });
            });
        }
    }
}

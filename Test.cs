using Bonsai;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using OpenCV.Net;

#if DEBUG
namespace PSTH
{
    [Combinator]
    [Description("")]
    [WorkflowElementCategory(ElementCategory.Transform)]
    public class Test
    {
        public int TestInt
        {
            get => _testInt;
            set
            {
                _testInt = value;
                _testIntSubject.OnNext(value);
            }
        }

        private Subject<int> _testIntSubject = new Subject<int>();
        private int _testInt;

        public IObservable<int> Process(IObservable<int> source)
        {
            var i = 0;
            _testIntSubject.Subscribe(v => i = v);
            return source.Select(input =>
            {
                return i + input;
            });
        }
    }
}
#endif

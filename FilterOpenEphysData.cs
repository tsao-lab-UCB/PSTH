using Bonsai;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace PSTH
{
    [Combinator]
    [Description("Filter OpenEphysData to keep only the continuous, spike or event data.")]
    [WorkflowElementCategory(ElementCategory.Combinator)]
    public class FilterOpenEphysData
    {
        [Description("Type of the OpenEphysData to keep.")]
        public DataType Type { get; set; }

        public IObservable<Timestamped<OpenEphysData>> Process(IObservable<Timestamped<OpenEphysData>> source)
        {
            return source.Where(input => input.Value.Type == Type);
        }
    }
}

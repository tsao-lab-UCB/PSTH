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
        /// <summary>
        /// Type of the OpenEphysData to keep.
        /// </summary>
        [Description("Type of the OpenEphysData to keep.")]
        public DataType Type { get; set; }

        /// <summary>
        /// If unsorted spikes with SortedId = 0 are discarded
        /// </summary>
        [Description("If unsorted spikes with SortedId = 0 are discarded")]
        public bool SortedSpikeOnly { get; set; } = false;

        public IObservable<Timestamped<OpenEphysData>> Process(IObservable<Timestamped<OpenEphysData>> source)
        {
            return source.Where(input => input.Value.Type == Type && (!SortedSpikeOnly || input.Value.SortedId > 0));
        }
    }
}

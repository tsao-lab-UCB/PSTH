using Bonsai;
using NetMQ;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace PSTH
{
    [Combinator]
    [Description("")]
    [WorkflowElementCategory(ElementCategory.Combinator)]
    public class TrialEventParser
    {
        [Description("Filter trials based on trial outcome.")]
        public bool FilterOutcome { get; set; }

        private readonly Dictionary<int, string> _map = new Dictionary<int, string>();
        private readonly Regex _addConditionRx = 
            new Regex(@"Name (?<name>\S+) TrialTypes (?<types>[\d ]+)", RegexOptions.Compiled);

        public IObservable<Timestamped<string>> Process(IObservable<Timestamped<OpenEphysData>> source)
        {
            return Observable.Create<Timestamped<string>>(observer =>
            {
                return source.Subscribe(data =>
                {
                    var str = data.Value.Message;
                    if (string.IsNullOrEmpty(str)) return;
                    if (str.StartsWith("ClearDesign") || str.StartsWith("NewDesign"))
                    {
                        _map.Clear();
                    }
                    else if (str.StartsWith("AddCondition"))
                    {
                        var matches = _addConditionRx.Matches(str);
                        if (matches.Count == 0) return;
                        var groups = matches[0].Groups;
                        var name = groups["name"].Value;
                        var types = groups["types"].Value.Split(' ').Select(int.Parse);
                        foreach (var type in types)
                        {
                            _map[type] = name;
                        }
                    }
                    else if (str.StartsWith("TrialStart"))
                    {
                        var tokens = str.Split(' ');
                        if (tokens.Length != 2 || !int.TryParse(tokens[1], out var type) || !_map.TryGetValue(type, out var @class)) 
                            return;
                        observer.OnNext(new Timestamped<string>(@class, data.Timestamp));
                    }
                });
            });
        }
    }
}

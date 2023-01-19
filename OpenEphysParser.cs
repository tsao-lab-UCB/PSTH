using Bonsai;
using NetMQ;
using Newtonsoft.Json;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PSTH
{
    /// <summary>
    /// Represents an operator that converts message from ZMQInterface Plugin of OpenEphys to OpenEphysData
    /// </summary>
    [Combinator]
    [Description("Converts message from ZMQInterface Plugin of OpenEphys to OpenEphysData")]
    [WorkflowElementCategory(ElementCategory.Transform)]
    public class OpenEphysParser : Transform<NetMQMessage, Timestamped<OpenEphysData>>
    {
        /// <summary>
        /// Sampling rate of the continuous data stream in OpenEphys.
        /// It's automatically updated if the message contains continuous data. 
        /// </summary>
        [Description("Sampling rate of the continuous data stream in OpenEphys. " +
                     "It's automatically updated if the message contains continuous data.")]
        public ushort SamplingRate { get; set; } = 25000;

        ///// <summary>
        ///// Optional filter for the desired datatype. Null means no filtering
        ///// </summary>
        //[Obsolete, Browsable(false)]
        //[Description("Optional filter for the desired datatype. Null means no filtering")]
        //public DataType? Type { get; set; } = null;

        ///// <summary>
        ///// If unsorted spikes with SortedId = 0 are discarded
        ///// </summary>
        //[Obsolete, Browsable(false)]
        //[Description("If unsorted spikes with SortedId = 0 are discarded")]
        //public bool SortedSpikeOnly { get; set; } = false;

        ///// <summary>
        ///// If the sample number in the original data is used to generate the timestamp.
        ///// This is useful when you don't have to sync with external data.
        ///// </summary>
        //[Obsolete, Browsable(false)]
        //[Description("If the sample number in the original data is used to generate the timestamp. " +
        //             "This is useful when you don't have to sync with external data.")]
        //public bool UseOriginalTimestamp { get; set; } = true;

        /// <summary>
        /// Converts message from ZMQInterface Plugin of OpenEphys to OpenEphysData
        /// </summary>
        /// <param name="source">The source sequence from an ZeroMQ Source</param>
        /// <returns>A sequence of standard timestamped OpenEphysData</returns>
        public override IObservable<Timestamped<OpenEphysData>> Process(IObservable<NetMQMessage> source)
        {
            var startTimeSet = false;
            var startTime = default(DateTimeOffset);
            return source.Select(input =>
            {
                if (input.FrameCount != 3)
                    return default;
                var header =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(input[1].ConvertToString());
                if (header == null || !header.TryGetValue("type", out var type))
                    return default;
                var key = type.ToString() == "spike" ? "spike" : "content";
                var content = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    header[key].ToString());
                if (content == null) return default;
                var messageId = long.Parse(header["message_num"].ToString());
                var sampleNumber = long.Parse(content["sample_num"].ToString());
                var stream = content["stream"].ToString();
                if (!startTimeSet)
                {
                    startTime = DateTimeOffset.Now - TimeSpan.FromSeconds((double) sampleNumber / SamplingRate);
                    startTimeSet = true;
                }

                //var timeStamp = UseOriginalTimestamp
                //    ? startTime + TimeSpan.FromSeconds((double) sampleNumber / SamplingRate)
                //    : HighResolutionScheduler.Now;
                var timeStamp = startTime + TimeSpan.FromSeconds((double)sampleNumber / SamplingRate);
                ushort sampleCount;
                Mat data;
                switch (type.ToString())
                {
                    case "data":
                        sampleCount = ushort.Parse(content["num_samples"].ToString());
                        var samplingRate = ushort.Parse(content["sample_rate"].ToString());
                        if (samplingRate != SamplingRate)
                        {
                            SamplingRate = samplingRate;
                            startTime = DateTimeOffset.Now - TimeSpan.FromSeconds((double) sampleNumber / SamplingRate);
                        }

                        data = new Mat(1, sampleCount, Depth.F32, 1);
                        Marshal.Copy(input[2].Buffer, 0, data.Data, sampleCount * 4);
                        timeStamp += TimeSpan.FromSeconds((double) sampleCount / SamplingRate);
                        return new Timestamped<OpenEphysData>(new OpenEphysData(
                            //timeStamp,
                            messageId,
                            sampleNumber,
                            stream,
                            ushort.Parse(content["channel_num"].ToString()),
                            sampleCount,
                            samplingRate,
                            data
                        ), timeStamp);
                    case "spike":
                        sampleCount = ushort.Parse(content["num_samples"].ToString());
                        var channelCount = ushort.Parse(content["num_channels"].ToString());
                        data = new Mat(channelCount, sampleCount, Depth.F32, 1);
                        Marshal.Copy(input[2].Buffer, 0, data.Data, channelCount * sampleCount * 4);
                        return new Timestamped<OpenEphysData>(new OpenEphysData(
                            //timeStamp,
                            messageId,
                            sampleNumber,
                            stream,
                            content["electrode"].ToString(),
                            byte.Parse(content["source_node"].ToString()),
                            channelCount,
                            sampleCount,
                            ushort.Parse(content["sorted_id"].ToString()),
                            data
                        ), timeStamp);
                    case "event":
                        if (input[2].Buffer.Length != 10) return default;
                        return new Timestamped<OpenEphysData>(new OpenEphysData(
                            //timeStamp,
                            messageId,
                            sampleNumber,
                            stream,
                            byte.Parse(content["source_node"].ToString()),
                            byte.Parse(content["type"].ToString()),
                            input[2].Buffer[0],
                            input[2].Buffer[1],
                            BitConverter.ToUInt64(input[2].Buffer, 2)
                        ), timeStamp);
                    default:
                        return default;
                }
            }).Where(data => data.Value != null);
        }
    }
}

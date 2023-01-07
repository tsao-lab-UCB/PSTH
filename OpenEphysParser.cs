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
    public enum DataType
    {
        Continuous,
        Spike,
        Event
    }

    public interface IOpenEphysData
    {
        DataType Type { get; }
        //DateTimeOffset TimeStamp { get; }
        long MessageId { get; }
        long SampleNumber { get; }
        string Stream { get; }
    }

    public interface IContinuousData : IOpenEphysData
    {
        ushort Channel { get; }
        ushort SampleCount { get; }
        ushort SamplingRate { get; }
        Mat Data { get; }
    }

    public interface ISpikeData : IOpenEphysData
    {
        string Electrode { get; }
        byte NodeId { get; }
        ushort Channel { get; }
        ushort ChannelCount { get; }
        ushort SampleCount { get; }
        ushort SortedId { get; }
        Mat Data { get; }
    }

    public interface IEventData : IOpenEphysData
    {
        byte NodeId { get; }
        byte EventType { get; }
        byte EventLine { get; }
        byte EventState { get; }
        ulong EventWord { get; }
    }

    public class OpenEphysData : IOpenEphysData, IContinuousData, ISpikeData, IEventData
    {
        public DataType Type { get; }
        //public DateTimeOffset TimeStamp { get; }
        public long MessageId { get; }
        public long SampleNumber { get; }
        public string Stream { get; }
        public string Electrode { get; }
        public byte NodeId { get; }
        public byte EventType { get; }
        public byte EventLine { get; }
        public byte EventState { get; }
        public ulong EventWord { get; }
        public ushort Channel { get; }
        public ushort ChannelCount { get; }
        public ushort SampleCount { get; }
        public ushort SamplingRate { get; }
        public ushort SortedId { get; }
        public Mat Data { get; }

        public OpenEphysData(long messageId, long sampleNumber, string stream, ushort channel,
            ushort sampleCount, ushort samplingRate, Mat data)
        {
            Type = DataType.Continuous;
            //TimeStamp = timeStamp;
            MessageId = messageId;
            SampleNumber = sampleNumber;
            Stream = stream;
            Channel = channel;
            SampleCount = sampleCount;
            SamplingRate = samplingRate;
            Data = data;
        }

        public OpenEphysData(long messageId, long sampleNumber, string stream,
            string electrode, byte nodeId, ushort channelCount, ushort sampleCount, ushort sortedId, Mat data)
        {
            Type = DataType.Spike;
            //TimeStamp = timeStamp;
            MessageId = messageId;
            SampleNumber = sampleNumber;
            Stream = stream;
            Electrode = electrode;
            NodeId = nodeId;
            ChannelCount = channelCount;
            SampleCount = sampleCount;
            SortedId = sortedId;
            Data = data;
        }

        public OpenEphysData(long messageId, long sampleNumber, string stream, byte nodeId,
            byte eventType, byte eventLine, byte eventState, ulong eventWord)
        {
            Type = DataType.Event;
            //TimeStamp = timeStamp;
            MessageId = messageId;
            SampleNumber = sampleNumber;
            Stream = stream;
            NodeId = nodeId;
            EventType = eventType;
            EventLine = eventLine;
            EventState = eventState;
            EventWord = eventWord;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case DataType.Continuous:
                    return $"[{MessageId}] LFP: channel {Channel}, {SampleCount} samples, {SamplingRate} Hz. ";
                case DataType.Spike:
                    return $"[{MessageId}] Spike: {Electrode}, Id {SortedId}. ";
                case DataType.Event:
                    return $"[{MessageId}] Event: line {EventLine}, {(EventState > 0 ? "HIGH" : "LOW")}. ";
                default:
                    return base.ToString();
            }
        }
    }

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

        /// <summary>
        /// Optional filter for the desired datatype. Null means no filtering
        /// </summary>
        [Description("Optional filter for the desired datatype. Null means no filtering")]
        public DataType? Type { get; set; } = null;

        /// <summary>
        /// If unsorted spikes with SortedId = 0 are discarded
        /// </summary>
        [Description("If unsorted spikes with SortedId = 0 are discarded")]
        public bool SortedSpikeOnly { get; set; } = false;

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

                //var timeStamp = startTime + TimeSpan.FromSeconds((double)sampleNumber / SamplingRate);
                var timeStamp = HighResolutionScheduler.Now;
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
            }).Where(data => data.Value != null 
                             && (Type == null || data.Value.Type == Type) 
                             && (!SortedSpikeOnly || data.Value.SortedId != 0));
        }
    }
}

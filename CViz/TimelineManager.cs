﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using CViz.Config;
using CViz.Control;
using CViz.Timeline;
using CViz.Timeline.Triggers;
using log4net;
using StilSoft.CasparCG.AmcpClient;

namespace CViz
{
    class TimelineManager
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TimelineManager));

        public static string TimelineExt = ".tl";

        private readonly AmcpConnection _client;
        private readonly Config.Config _config;
        private readonly Dictionary<string, Timeline.Timeline> _timelines;

        private IControlInterface _controlInterface;

        public TimelineManager(Config.Config config)
        {
            _config = config;
            _timelines = new Dictionary<string, Timeline.Timeline>();

            OscWrapper oscWrapper = new OscWrapper(this, config.OscPort);
            new Thread(oscWrapper.Run).Start();

            Log.InfoFormat("Connecting to CasparCG at {0}:{1}", config.CasparHost, config.CasparPort);
            _client = new AmcpConnection(config.CasparHost, config.CasparPort)
            {
                AutoConnect = true,
                AutoReconnect = true,
                KeepAliveEnable = true
            };

            foreach (SlotConfig slot in config.Slots)
            {
                var state = new TimelineState(null, slot.Id, "", "");
                var tl = new Timeline.Timeline(slot.Id, _client, slot.Channel, state,
                    new TimelineSpec("", new List<ITrigger>(), new Dictionary<string, IEnumerable<ITrigger>>()));
                state.SetState(TimelineState.StateType.Clear);
                _timelines.Add(slot.Id, tl);
            }
        }

        public void BindInterface(IControlInterface newInterface)
        {
            Log.InfoFormat("Binding control interface");
            _controlInterface = newInterface;
        }

        public IReadOnlyList<TimelineState> GetCompleteState()
        {
            lock (_timelines)
            {
                return _timelines.Select(t => t.Value.State).ToList();
            }
        }

        public TimelineState GetStateForTimelineSlot(string timelineSlot)
        {
            lock (_timelines)
            {
                return _timelines.Select(t => t.Value.State).FirstOrDefault(s => s.TimelineSlot == timelineSlot);
            }
        }
        
        public bool LoadTimeline(string slot, string filename, string instanceId)
        {
            lock (_timelines)
            {
                if (!_timelines.TryGetValue(slot, out Timeline.Timeline timeline))
                {
                    Log.ErrorFormat("Channel {0} not defined in the config", slot);
                    return false;
                }
                if (timeline.IsRunning)
                {
                    Log.WarnFormat("Cannot load timeline to {0} with one already running", slot);
                    return false;
                }
                
                TimelineSpec spec;
                try
                {
                    spec = Parser.ParseFile(_config.TemplateDir, filename);
                }
                catch (Exception)
                {
                    Log.ErrorFormat("Faield to load timeline file: {0}", filename);
                    return false;
                }

                TimelineState state = new TimelineState(_controlInterface, slot, filename, instanceId);
                _timelines[slot] = new Timeline.Timeline(slot, _client, timeline.ChannelNumber, state, spec);

                Log.InfoFormat("Timeline {0} ({1}) ready", slot, filename);

                return true;
            }
        }

        public bool StartTimeline(string slot, ImmutableDictionary<string, string> parameters)
        {
            lock (_timelines)
            {
                Timeline.Timeline timeline = _timelines[slot];
                if (timeline == null || timeline.IsRunning)
                    return false;

                timeline.SetParameterValues(parameters);

                Log.InfoFormat("Starting timeline {0}", slot);

                new Thread(timeline.Run).Start();
                return true;
            }
        }

        public void KillTimeline(string slot)
        {
            lock (_timelines)
            {
                if (_timelines.TryGetValue(slot, out var timeline))
                    timeline.Kill();
            }
        }

        public void TriggerCue(string slot)
        {
            lock (_timelines)
            {
                if (_timelines.TryGetValue(slot, out var timeline))
                    timeline.TriggerCue();
            }
        }

        internal void TriggerOnVideoFrame(int channel, int layer, long frame, long totalFrames)
        {
            lock (_timelines)
            {
                foreach (Timeline.Timeline timeline in _timelines.Values.Where(t => t.ChannelNumber == channel))
                {
                    timeline.TriggerOnVideoFrame(layer, frame, totalFrames);
                }
            }
        }

        internal void TriggerOnChannelFrame(int channel, int port, long frame)
        {
            lock (_timelines)
            {
                foreach (Timeline.Timeline timeline in _timelines.Values.Where(t => t.ChannelNumber == channel))
                {
                    timeline.TriggerOnChannelFrame(port, frame);
                }
            }
        }

        public void TriggerChild(string timelineId, string name, Dictionary<string, string> parameters)
        {
            lock (_timelines)
            {
                if (_timelines.TryGetValue(timelineId, out var timeline))
                    timeline.TriggerChild(name, parameters);
            }
        }
    }
}
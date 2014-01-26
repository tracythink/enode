﻿using System.Threading.Tasks;
using ECommon.IoC;
using ECommon.Logging;
using ECommon.Serializing;
using ECommon.Socketing;
using ENode.Commanding;
using EQueue.Clients.Producers;
using EQueue.Protocols;

namespace ENode.EQueue
{
    public class CommandService : ICommandService
    {
        private readonly ILogger _logger;
        private readonly IBinarySerializer _binarySerializer;
        private readonly ICommandTopicProvider _commandTopicProvider;
        private readonly ICommandTypeCodeProvider _commandTypeCodeProvider;
        private readonly Producer _producer;

        public CommandService() : this(ProducerSetting.Default) { }
        public CommandService(ProducerSetting setting) : this(string.Format("CommandService@{0}", SocketUtils.GetLocalIPV4()), setting) { }
        public CommandService(string id, ProducerSetting setting)
        {
            _producer = new Producer(id, setting);
            _binarySerializer = ObjectContainer.Resolve<IBinarySerializer>();
            _commandTopicProvider = ObjectContainer.Resolve<ICommandTopicProvider>();
            _commandTypeCodeProvider = ObjectContainer.Resolve<ICommandTypeCodeProvider>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().Name);
        }

        public CommandService Start()
        {
            _producer.Start();
            return this;
        }
        public CommandService Shutdown()
        {
            _producer.Shutdown();
            return this;
        }
        public Task<CommandResult> Send(ICommand command)
        {
            var raw = _binarySerializer.Serialize(command);
            var topic = _commandTopicProvider.GetTopic(command);
            var typeCode = _commandTypeCodeProvider.GetTypeCode(command);
            var data = PayloadUtils.EncodePayload(new Payload(raw, typeCode));
            var message = new Message(topic, data);
            var taskCompletionSource = new TaskCompletionSource<CommandResult>();
            _producer.SendAsync(message, command.AggregateRootId).ContinueWith(sendTask =>
            {
                if (sendTask.Result.SendStatus == SendStatus.Success)
                {
                    taskCompletionSource.SetResult(CommandResult.Success);
                }
                else
                {
                    taskCompletionSource.SetResult(new CommandResult("Send command failed."));
                }
            });
            return taskCompletionSource.Task;
        }
    }
}

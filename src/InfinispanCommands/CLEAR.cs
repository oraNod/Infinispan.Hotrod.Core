﻿using System;
using System.Collections.Generic;
using System.Text;
using BeetleX.Buffers;

namespace Infinispan.Hotrod.Core.Commands
{
    public class CLEAR : Command
    {
        public CLEAR()
        {
            NetworkReceive = OnReceive;
        }
        public int TimeOut { get; set; }
        public override string Name => "CLEAR";

        public override Byte Code => 0x13;
        public override void OnExecute(CommandContext ctx)
        {
            base.OnExecute(ctx);
        }

        public override void Execute(CommandContext ctx, InfinispanClient client, PipeStream stream)
        {
            base.Execute(ctx, client, stream);
        }
        public override Result OnReceive(InfinispanRequest request, PipeStream stream)
        {
            return new Result{ Status =  ResultStatus.Completed, ResultType = ResultType.Null };
        }
    }
}

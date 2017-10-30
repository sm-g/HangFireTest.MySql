using System;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;

namespace HangFireTest.MySql
{
    public class MyFilter : JobFilterAttribute, IClientFilter, IServerFilter
    {
        private Func<int> importIdGetter = () => 2;

        public void OnCreating(CreatingContext filterContext)
        {
            if (filterContext == null) throw new ArgumentNullException(nameof(filterContext));

            filterContext.SetJobParameter(
                "ImportId", importIdGetter());
        }

        public void OnCreated(CreatedContext filterContext)
        {
        }

        public void OnPerforming(PerformingContext filterContext)
        {
            var importId = filterContext.GetJobParameter<int>("ImportId");

            filterContext.Items["haha"] = importId;

            // set current import id...
        }

        public void OnPerformed(PerformedContext filterContext)
        {
        }
    }
}
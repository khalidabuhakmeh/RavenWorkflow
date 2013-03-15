using System;
using Raven.Client.Embedded;
using Raven.Imports.Newtonsoft.Json;
using Stateless;

namespace RavenWorkflow.App
{
    class Program
    {
        static void Main(string[] args)
        {
            var documentStore = (EmbeddableDocumentStore)new EmbeddableDocumentStore
            {
                UseEmbeddedHttpServer = true,
            }.Initialize();

            var bug = new Bug("Incorrect stock count");
            using (var session = documentStore.OpenSession())
            {
                session.Store(bug);
                session.SaveChanges();
            }

            using (var session = documentStore.OpenSession())
            {
                var reloaded = session.Load<Bug>(bug.Id);
                reloaded.Assign("Joe");
                reloaded.Defer();
                reloaded.Assign("Harry");
                reloaded.Assign("Fred");
                reloaded.Close();
                session.SaveChanges();
            }

            Console.ReadKey(false);
        }
    }

    public class Bug
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Assignee { get; set; }

        public States State { get; protected set; }

        public enum States { Open, Assigned, Deferred, Resolved, Closed }
        public enum Triggers { Assign, Defer, Resolve, Close }

        [JsonIgnore]
        protected StateMachine<States, Triggers> Machine { get; set; }
        [JsonIgnore]
        protected StateMachine<States, Triggers>.TriggerWithParameters<string> AssignTrigger { get; set; }

        protected Bug()
        {            
            Machine = new StateMachine<States, Triggers>(() => State, s => State = s);
            AssignTrigger = Machine.SetTriggerParameters<string>(Triggers.Assign);

            Machine.Configure(States.Open)
               .Permit(Triggers.Assign, States.Assigned);

            Machine.Configure(States.Assigned)
                .SubstateOf(States.Open)
                .OnEntryFrom(AssignTrigger, OnAssigned)
                .PermitReentry(Triggers.Assign)
                .Permit(Triggers.Close, States.Closed)
                .Permit(Triggers.Defer, States.Deferred)
                .OnExit(OnDeassigned);

            Machine.Configure(States.Deferred)
                .OnEntry(() => Assignee = null)
                .Permit(Triggers.Assign, States.Assigned);
        }

        public Bug(string title)
            : this()
        {
            Title = title;
        }

        public void Close()
        {
            Machine.Fire(Triggers.Close);
        }

        public void Assign(string assignee)
        {
            Machine.Fire(AssignTrigger, assignee);
        }

        public bool CanAssign
        {
            get
            {
                return Machine.CanFire(Triggers.Assign);
            }
        }

        public void Defer()
        {
            Machine.Fire(Triggers.Defer);
        }

        void OnAssigned(string assignee)
        {
            if (Assignee != null && assignee != Assignee)
                SendEmailToAssignee("Don't forget to help the new guy.");

            Assignee = assignee;
            SendEmailToAssignee("You own it.");
        }

        void OnDeassigned()
        {
            SendEmailToAssignee("You're off the hook.");
        }

        void SendEmailToAssignee(string message)
        {
            Console.WriteLine("{0}, RE {1}: {2}", Assignee, Title, message);
        }
    }
}

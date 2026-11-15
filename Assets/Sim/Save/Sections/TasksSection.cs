using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Tasks save section (decision 0034, 0005): the id counter and board version, then every task record: def id,
    /// the def's steps, state, owner, assignee, target tree, priority, subject, ox, site, current step, step progress,
    /// the awaited Fell task, the winding-down flag and the trees it holds reserved. The def's policy (who may take it,
    /// the required resource) is not saved; it is looked up by def id in <see cref="TaskDefCatalog"/>. The busy set is
    /// rebuilt from the Assigned and Active tasks.
    /// </summary>
    public sealed class TasksSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 7;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            TaskBoard board = world.Tasks;
            w.WriteInt32(board.NextId);
            w.WriteInt32(board.Version);
            w.WriteInt32(board.Count);
            foreach (TaskRecord t in board.All)
            {
                w.WriteInt32(t.Id);
                w.WriteString(t.Def.Id);
                w.WriteInt32(t.Def.Steps.Count);
                foreach (TaskStep s in t.Def.Steps) WriteStep(w, s);
                w.WriteInt32((int)t.State);
                w.WriteInt32(t.OwnerOperationId);
                WriteOptInt(w, t.AssigneeId);
                WriteOptInt(w, t.Target.HasValue ? t.Target.Value.Value : (int?)null);
                w.WriteInt32((int)t.Priority);
                WriteOptString(w, t.Subject);
                WriteOptInt(w, t.OxId);
                w.WriteBool(t.OxWasAssigned);
                WriteOptInt(w, t.SiteFootprintId);
                w.WriteInt32(t.CurrentStep);
                w.WriteInt32(t.StepProgress);
                WriteOptInt(w, t.AwaitsTaskId);
                w.WriteBool(t.WindingDown);
                w.WriteInt32(t.Reservations.Count);
                foreach (TreeId tree in t.Reservations) w.WriteInt32(tree.Value);
            }
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            try
            {
                ReadInto(world, r);
            }
            catch (InvalidOperationException e) { throw new SaveFormatException(e.Message); }
            catch (ArgumentException e) { throw new SaveFormatException(e.Message); }
        }

        private static void ReadInto(SimWorld world, SaveReader r)
        {
            TaskBoard board = world.Tasks;
            board.RestoreCounters(r.ReadInt32(), r.ReadInt32());
            int count = r.ReadCount(60);
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt32();
                string defId = r.ReadString();
                int nSteps = r.ReadCount(30);
                var steps = new List<TaskStep>(nSteps);
                for (int k = 0; k < nSteps; k++) steps.Add(ReadStep(r));
                TaskDef def = TaskDefCatalog.Build(defId, steps);
                if (def == null) throw new SaveFormatException("Task " + id + " has unknown def id '" + defId + "'.");
                int state = r.ReadInt32();
                int owner = r.ReadInt32();
                int? assignee = ReadOptInt(r);
                int? target = ReadOptInt(r);
                int priority = r.ReadInt32();
                string subject = ReadOptString(r);
                int? ox = ReadOptInt(r);
                bool oxWasAssigned = r.ReadBool();
                int? site = ReadOptInt(r);
                int step = r.ReadInt32();
                int progress = r.ReadInt32();
                int? awaits = ReadOptInt(r);
                bool winding = r.ReadBool();
                int nRes = r.ReadCount(4);
                var trees = new List<TreeId>(nRes);
                for (int k = 0; k < nRes; k++) trees.Add(new TreeId(r.ReadInt32()));

                if (!Enum.IsDefined(typeof(TaskState), state)) throw new SaveFormatException("Bad task state " + state + ".");
                if (!Enum.IsDefined(typeof(TaskPriority), priority)) throw new SaveFormatException("Bad task priority " + priority + ".");
                if (step < 0 || step > nSteps || progress < 0) throw new SaveFormatException("Bad step position on task " + id + ".");
                if (assignee.HasValue && world.People.FindCitizen(assignee.Value) == null)
                    throw new SaveFormatException("Task " + id + " names a missing citizen " + assignee.Value + ".");
                if (ox.HasValue && world.People.FindOx(ox.Value) == null)
                    throw new SaveFormatException("Task " + id + " names a missing ox " + ox.Value + ".");

                var t = new TaskRecord(id, def, owner, target.HasValue ? new TreeId(target.Value) : (TreeId?)null,
                    (TaskPriority)priority, subject)
                {
                    State = (TaskState)state, AssigneeId = assignee, CurrentStep = step, StepProgress = progress,
                    OxId = ox, OxWasAssigned = oxWasAssigned, SiteFootprintId = site, AwaitsTaskId = awaits,
                    WindingDown = winding
                };
                board.RestoreTask(t, trees);
            }
            foreach (TaskRecord t in board.All)
                if (t.AwaitsTaskId.HasValue && board.Get(t.AwaitsTaskId.Value) == null)
                    throw new SaveFormatException("Task " + t.Id + " awaits missing task " + t.AwaitsTaskId.Value + ".");
        }

        private static void WriteStep(SaveWriter w, TaskStep s)
        {
            w.WriteInt32((int)s.Kind);
            w.WriteInt32(s.DurationTicks);
            w.WriteBool(s.Heavy);
            WriteOptString(w, s.StageId);
            w.WriteDouble(s.X);
            w.WriteDouble(s.Z);
            w.WriteInt32(s.LocationId);
            w.WriteInt32(s.Count);
        }

        private static TaskStep ReadStep(SaveReader r)
        {
            int kind = r.ReadInt32();
            int dur = r.ReadInt32();
            bool heavy = r.ReadBool();
            string stage = ReadOptString(r);
            double x = r.ReadDouble(), z = r.ReadDouble();
            int loc = r.ReadInt32(), count = r.ReadInt32();
            switch (kind)
            {
                case (int)StepKind.MoveTo: return TaskStep.MoveTo(heavy);
                case (int)StepKind.Work: return TaskStep.Work(dur, heavy, stage);
                case (int)StepKind.FetchOx: return TaskStep.FetchOx();
                case (int)StepKind.LeadTo: return TaskStep.LeadTo(new PathPoint(x, z));
                case (int)StepKind.LoadOx: return TaskStep.LoadOx(loc, count, dur, heavy);
                case (int)StepKind.UnloadOx: return TaskStep.UnloadOx(loc, count, dur, heavy);
                case (int)StepKind.ReturnOx: return TaskStep.ReturnOx();
                case (int)StepKind.WalkTo: return TaskStep.WalkTo(new PathPoint(x, z));
                case (int)StepKind.CollectFood: return TaskStep.CollectFood();
                case (int)StepKind.DepositFood: return TaskStep.DepositFood();
                default: throw new SaveFormatException("Bad task step kind " + kind + ".");
            }
        }

        private static void WriteOptInt(SaveWriter w, int? v)
        {
            w.WriteBool(v.HasValue);
            w.WriteInt32(v ?? 0);
        }

        private static int? ReadOptInt(SaveReader r)
        {
            bool has = r.ReadBool();
            int v = r.ReadInt32();
            return has ? v : (int?)null;
        }

        private static void WriteOptString(SaveWriter w, string s)
        {
            w.WriteBool(s != null);
            if (s != null) w.WriteString(s);
        }

        private static string ReadOptString(SaveReader r) => r.ReadBool() ? r.ReadString() : null;
    }
}

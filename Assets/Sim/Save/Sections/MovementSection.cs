using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Movement save section (decision 0034): every citizen's and ox's move order (target, path, segment index,
    /// progress, status, planned nav version), follow links, debug sends, the routine system's per-citizen state and the
    /// work day's cached plans. The <see cref="NavGrid"/> is not saved (the restore path rebuilds it); only its version
    /// number and the movement system's last-seen version are, so orders keep their planned route instead of
    /// re-planning on the first step after a load. An order shared by two holders (the citizen's <c>Move</c>, the
    /// routine state, a debug send) is written once and stays one object after the load.
    /// </summary>
    public sealed class MovementSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 6;

        private const byte RefNone = 0, RefSame = 1, RefOwn = 2;
        private const byte LeaderCitizen = 0, LeaderOx = 1;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            w.WriteInt32(world.Nav.Version);
            MovementSystem ms = world.FindSystem<MovementSystem>();
            w.WriteInt32(ms != null ? ms.SeenNavVersion : -1);

            w.WriteInt32(world.People.CitizenCount);
            foreach (Citizen c in world.People.Citizens)
            {
                w.WriteInt32(c.Id);
                WriteOrderRef(w, c.Move, null);
                WriteFollow(w, c.Follow, world);
                w.WriteBool(c.DebugSend != null);
                if (c.DebugSend != null)
                {
                    WriteOrderRef(w, c.DebugSend.Order, c.Move);
                    w.WriteInt64(c.DebugSend.LingerUntilTick);
                }
            }
            w.WriteInt32(world.People.OxCount);
            foreach (Ox o in world.People.Oxen)
            {
                w.WriteInt32(o.Id);
                WriteOrderRef(w, o.Move, null);
                WriteFollow(w, o.Follow, world);
            }

            RoutineSystem routine = world.FindSystem<RoutineSystem>();
            List<RoutineSystem.Saved> states = routine != null ? routine.ExportStates() : new List<RoutineSystem.Saved>();
            w.WriteInt32(states.Count);
            foreach (RoutineSystem.Saved s in states)
            {
                w.WriteInt32(s.CitizenId);
                w.WriteInt32(s.Phase);
                w.WriteInt64(s.WakeTick);
                w.WriteInt64(s.StayUntilTick);
                Citizen c = world.People.FindCitizen(s.CitizenId);
                WriteOrderRef(w, s.Order, c?.Move);
            }

            var plans = world.WorkDay.ExportPlans();
            w.WriteInt32(plans.Count);
            foreach (var p in plans)
            {
                w.WriteInt32(p.Id);
                w.WriteInt64(p.KeyDayStart);
                w.WriteInt64(p.Depart);
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
            world.Nav.RestoreVersion(r.ReadInt32());
            int seen = r.ReadInt32();
            MovementSystem ms = world.FindSystem<MovementSystem>();
            if (ms != null) ms.SeenNavVersion = seen;

            int nc = r.ReadCount(7);
            for (int i = 0; i < nc; i++)
            {
                Citizen c = world.People.FindCitizen(r.ReadInt32());
                if (c == null) throw new SaveFormatException("Movement section names a missing citizen.");
                c.Move = ReadOrderRef(r, null);
                c.Follow = ReadFollow(r, world, c);
                if (r.ReadBool())
                {
                    MoveOrder order = ReadOrderRef(r, c.Move);
                    if (order == null) throw new SaveFormatException("Debug send without an order.");
                    c.DebugSend = new DebugSendState(order) { LingerUntilTick = r.ReadInt64() };
                }
            }
            int no = r.ReadCount(6);
            for (int i = 0; i < no; i++)
            {
                Ox o = world.People.FindOx(r.ReadInt32());
                if (o == null) throw new SaveFormatException("Movement section names a missing ox.");
                o.Move = ReadOrderRef(r, null);
                o.Follow = ReadFollow(r, world, o);
            }

            RoutineSystem routine = world.FindSystem<RoutineSystem>();
            int ns = r.ReadCount(25);
            for (int i = 0; i < ns; i++)
            {
                int id = r.ReadInt32();
                int phase = r.ReadInt32();
                long wake = r.ReadInt64(), stay = r.ReadInt64();
                Citizen c = world.People.FindCitizen(id);
                if (c == null) throw new SaveFormatException("Routine state names a missing citizen " + id + ".");
                if (phase < 0 || phase > RoutineSystem.MaxPhase) throw new SaveFormatException("Bad routine phase " + phase + ".");
                MoveOrder order = ReadOrderRef(r, c.Move);
                if (routine != null) routine.RestoreState(new RoutineSystem.Saved(id, phase, wake, stay, order));
            }

            int np = r.ReadCount(20);
            for (int i = 0; i < np; i++)
            {
                int id = r.ReadInt32();
                long key = r.ReadInt64(), depart = r.ReadInt64();
                world.WorkDay.RestorePlan(id, key, depart);
            }
        }

        private static void WriteOrderRef(SaveWriter w, MoveOrder order, MoveOrder agentMove)
        {
            if (order == null) { w.WriteByte(RefNone); return; }
            if (agentMove != null && ReferenceEquals(order, agentMove)) { w.WriteByte(RefSame); return; }
            w.WriteByte(RefOwn);
            w.WriteDouble(order.TargetX);
            w.WriteDouble(order.TargetZ);
            w.WriteInt32((int)order.Status);
            w.WriteString(order.FailReason);
            w.WriteInt32(order.SegmentIndex);
            w.WriteDouble(order.ProgressM);
            w.WriteInt32(order.PlannedNavVersion);
            w.WriteInt32(order.Path.Count);
            for (int i = 0; i < order.Path.Count; i++)
            {
                w.WriteDouble(order.Path[i].X);
                w.WriteDouble(order.Path[i].Z);
            }
        }

        private static MoveOrder ReadOrderRef(SaveReader r, MoveOrder agentMove)
        {
            byte kind = r.ReadByte();
            if (kind == RefNone) return null;
            if (kind == RefSame)
            {
                if (agentMove == null) throw new SaveFormatException("Order shared with a missing move order.");
                return agentMove;
            }
            if (kind != RefOwn) throw new SaveFormatException("Bad order reference " + kind + ".");
            double tx = r.ReadDouble(), tz = r.ReadDouble();
            int status = r.ReadInt32();
            string fail = r.ReadString();
            int seg = r.ReadInt32();
            double progress = r.ReadDouble();
            int planned = r.ReadInt32();
            int n = r.ReadCount(16);
            var pts = new PathPoint[n];
            for (int i = 0; i < n; i++) pts[i] = new PathPoint(r.ReadDouble(), r.ReadDouble());
            if (!Enum.IsDefined(typeof(MoveStatus), status)) throw new SaveFormatException("Bad move status " + status + ".");
            if (n > 0 ? seg < 0 || seg >= n : seg != 0) throw new SaveFormatException("Bad path segment index " + seg + ".");
            return new MoveOrder(tx, tz)
            {
                Status = (MoveStatus)status,
                FailReason = fail,
                Path = pts,
                SegmentIndex = seg,
                ProgressM = progress,
                PlannedNavVersion = planned
            };
        }

        private static void WriteFollow(SaveWriter w, FollowLink link, SimWorld world)
        {
            w.WriteBool(link != null);
            if (link == null) return;
            if (link.Leader is Citizen c) { w.WriteByte(LeaderCitizen); w.WriteInt32(c.Id); }
            else if (link.Leader is Ox o) { w.WriteByte(LeaderOx); w.WriteInt32(o.Id); }
            else throw new InvalidOperationException("Follow leader is neither a citizen nor an ox.");
            w.WriteDouble(link.OffsetM);
        }

        private static FollowLink ReadFollow(SaveReader r, SimWorld world, IMovable follower)
        {
            if (!r.ReadBool()) return null;
            byte kind = r.ReadByte();
            int id = r.ReadInt32();
            double offset = r.ReadDouble();
            IMovable leader = kind == LeaderCitizen ? (IMovable)world.People.FindCitizen(id)
                : kind == LeaderOx ? world.People.FindOx(id) : null;
            if (leader == null) throw new SaveFormatException("Follow link names a missing leader.");
            if (ReferenceEquals(leader, follower)) throw new SaveFormatException("An agent follows itself.");
            return new FollowLink(leader, offset);
        }
    }
}

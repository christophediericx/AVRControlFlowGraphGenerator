namespace AVRControlFlowGraphGenerator
{
    public class Relation
    {
        public int Source { get; }
        public int Target { get; }

        public Relation(int source, int target)
        {
            Source = source;
            Target = target;
        }

        protected bool Equals(Relation other)
        {
            return Source == other.Source && Target == other.Target;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((Relation) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Source * 397) ^ Target;
            }
        }
    }
}

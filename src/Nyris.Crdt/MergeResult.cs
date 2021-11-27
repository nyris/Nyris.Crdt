namespace Nyris.Crdt
{
    public enum MergeResult
    {
        Identical,
        ConflictSolved,
        NotUpdated // this result is for Delta CRDTs, where it is useful to know if "our" (full) instance
                   // was not updated by "their" (delta) update
    }
}
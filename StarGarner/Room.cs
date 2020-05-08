using System;

namespace StarGarner {
    internal class Room : IComparable<Room> {
        internal readonly String roomUrlKey;
        internal readonly Int64 roomId;
        internal readonly Boolean isSeed;
        internal readonly Int64 startedAt;

        internal Room(Int64 roomId, String roomUrlKey, Boolean isSeed, Int64 startedAt) {
            this.roomId = roomId;
            this.roomUrlKey = roomUrlKey;
            this.isSeed = isSeed;
            this.startedAt = startedAt;
        }

        // デフォルトのソート順は startedAt の降順
        public Int32 CompareTo(Room other)
            => other.startedAt.CompareTo( this.startedAt );
    }
}

using StarGarner.Util;
using System;

namespace StarGarner.Model {
    // ストリーミング情報
    internal class StreamingInfo : IComparable<StreamingInfo> {
        public String url;
        public String type;
        public Int64 quality;
        public Boolean is_default;

        public StreamingInfo(
            String url,
            String type,
            Boolean is_default,
            Int64 quality
            ) {
            this.url = url;
            this.type = type;
            this.is_default = is_default;
            this.quality = quality;
        }

        public Int32 CompareTo(StreamingInfo other)
            => other.is_default.CompareTo( is_default ).notZero()
            ?? other.quality.CompareTo( quality );
    }
}

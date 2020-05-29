using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace StarGarner.Util {

    // データの保存など、並行性があるのが好ましくない処理に使う
    public class SingleTask : IDisposable {
        static readonly Log log = new Log( "SingleTask" );

        private readonly Channel<Action> channel = Channel.CreateUnbounded<Action>( new UnboundedChannelOptions() {
            SingleReader = true
        } );

        internal async void add(Action action) {
            try {
                await channel.Writer.WriteAsync( action ).ConfigureAwait( false );
            } catch (Exception ex) {
                log.e( ex, "channel write error." );
            }
        }

        internal void complete() {
            try {
                channel.Writer.Complete();
            } catch (Exception ex) {
                log.e( ex, "channel complete error." );
            }
        }

        public void Dispose() => complete();

        public SingleTask() => Task.Run( async () => {
            try {
                while (await channel.Reader.WaitToReadAsync().ConfigureAwait( false )) {
                    while (channel.Reader.TryRead( out var item )) {
                        try {
                            item.Invoke();
                        } catch (Exception ex) {
                            log.e( ex, "action error." );
                        }
                    }
                }
            } catch (Exception ex) {
                log.e( ex, "channel read error." );
            }
        } );
    }
}

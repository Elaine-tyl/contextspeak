using System;
using System.Linq;

namespace ContextSpeak.Slice
{
    /// Preview a turn on copies. A required correction must pass before any purchase is committed.
    /// Only order-related state is copied; time, fishing and other ongoing progress remain untouched.
    public sealed class WaiterProposal
    {
        public readonly WaiterTurn Turn;
        private readonly SliceSave owner;
        private readonly OrderCart originalCart, cart;
        private readonly WaiterBrain originalWaiter, waiter;
        private readonly Wallet wallet;
        private readonly string signature;
        private bool used;

        public WaiterProposal(SliceSave save, string text)
        {
            owner = save;
            originalCart = save.Cart;
            originalWaiter = save.Waiter;
            signature = Signature(save);
            cart = new OrderCart
            {
                Lines = WaiterBrain.Restore(WaiterBrain.Save(save.Cart.Lines)),
                Status = save.Cart.Status, Payers = save.Cart.Payers, Table = save.Cart.Table,
            };
            waiter = new WaiterBrain
            {
                Stage = save.Waiter.Stage, Band = save.Waiter.Band, Name = save.Waiter.Name,
                LastOrder = save.Waiter.LastOrder.ToArray(), Turn = save.Waiter.Turn,
                Visits = save.Waiter.Visits, LastReceipt = save.Waiter.LastReceipt,
            };
            wallet = new Wallet { Cash = save.Wallet.Cash, Card = save.Wallet.Card };
            Turn = waiter.Respond(text, cart, wallet, save.Clock.MinuteOfDay);
        }

        public bool Commit(SliceSave save)
        {
            if (used || !ReferenceEquals(save, owner) || !ReferenceEquals(save.Cart, originalCart)
                || !ReferenceEquals(save.Waiter, originalWaiter) || signature != Signature(save)) return false;
            used = true;
            save.Cart = cart;
            save.Waiter = waiter;
            save.Wallet = wallet;
            return true;
        }

        private static string Signature(SliceSave save) => string.Join("|", new[]
        {
            save.Waiter.Turn.ToString(), save.Waiter.Stage.ToString(), save.Waiter.Band.ToString(), save.Cart.Status.ToString(),
            save.Cart.Payers.ToString(), save.Cart.Table, save.Wallet.Cash.ToString(), save.Wallet.Card.ToString(),
            string.Join("/", WaiterBrain.Save(save.Cart.Lines)),
        });
    }
}

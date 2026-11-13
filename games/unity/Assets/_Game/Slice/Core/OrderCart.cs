using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextSpeak.Slice
{
    /// One line in the cart. Choices are stored as "Group:choice" strings so JsonUtility can save them.
    [Serializable]
    public sealed class CartLine
    {
        public string Item;
        public int Quantity = 1;
        public string[] Choices = new string[0];

        public MenuItem Menu => CafeMenu.Find(Item);
        public string Choice(OptionGroup group)
        {
            var prefix = group + ":";
            var found = Choices.FirstOrDefault(entry => entry.StartsWith(prefix, StringComparison.Ordinal));
            return found?.Substring(prefix.Length);
        }
        public void Set(OptionGroup group, string choice)
        {
            if (!MenuOptions.Choices[group].Contains(choice)) throw new ArgumentException("Unknown choice " + choice);
            if (Menu == null || !Menu.Has(group)) return;
            var prefix = group + ":";
            Choices = Choices.Where(entry => !entry.StartsWith(prefix, StringComparison.Ordinal)).Append(prefix + choice).ToArray();
            // Hot drinks never have ice; iced drinks keep their ice setting.
            if (group == OptionGroup.Temperature && choice == "hot") Choices = Choices.Where(entry => !entry.StartsWith("Ice:", StringComparison.Ordinal)).ToArray();
        }
        /// Groups the waiter still has to ask about (temperature and size have no silent default).
        public IEnumerable<OptionGroup> Missing()
        {
            var menu = Menu;
            if (menu == null) yield break;
            foreach (var group in menu.Options)
            {
                if (Choice(group) != null || MenuOptions.Default(group) != null) continue;
                yield return group;
            }
        }
        public string Effective(OptionGroup group) => Choice(group) ?? MenuOptions.Default(group);
        public int UnitPrice
        {
            get
            {
                var menu = Menu;
                if (menu == null) return 0;
                return menu.Price + menu.Options.Sum(group => MenuOptions.Surcharge(Effective(group) ?? ""));
            }
        }
        public int Total => UnitPrice * Quantity;
        public string Describe()
        {
            var menu = Menu;
            if (menu == null) return Item;
            var parts = new List<string>();
            foreach (var group in new[] { OptionGroup.Size, OptionGroup.Temperature }) if (Choice(group) != null) parts.Add(Choice(group));
            var head = (Quantity > 1 ? Quantity + " × " : "") + string.Join(" ", parts.Append(Spoken(menu.Name)));
            var extras = menu.Options.Where(group => group != OptionGroup.Size && group != OptionGroup.Temperature)
                .Select(group => Choice(group)).Where(choice => choice != null && choice != MenuOptions.Default(OptionGroupOf(choice)))
                .ToArray();
            return extras.Length == 0 ? head : head + " with " + string.Join(" and ", extras);
        }
        /// Menu names mid-sentence: lower case except proper nouns.
        public static string Spoken(string name) => name.ToLowerInvariant().Replace("english", "English");
        private static OptionGroup OptionGroupOf(string choice) => MenuOptions.Choices.First(entry => entry.Value.Contains(choice)).Key;
    }

    public enum OrderStatus { Browsing, Confirmed, Paid, Preparing, Ready, Delivered, Finished }

    [Serializable]
    public sealed class Receipt
    {
        public string Id;
        public string[] Lines = new string[0];
        public int Total, Paid, Payers = 1;
        public string Method; // "card" or "cash"
        public int GameMinute;
    }

    /// The player's current order. All money and state changes happen here, never in AI output.
    [Serializable]
    public sealed class OrderCart
    {
        public List<CartLine> Lines = new List<CartLine>();
        public OrderStatus Status = OrderStatus.Browsing;
        public int Payers = 1;
        public string Table = "";

        public int Total => Lines.Sum(line => line.Total);
        public bool Empty => Lines.Count == 0;
        public bool Editable => Status == OrderStatus.Browsing;

        public CartLine Add(string itemId, int quantity = 1)
        {
            if (!Editable) throw new InvalidOperationException("The order is already confirmed.");
            var menu = CafeMenu.Find(itemId) ?? throw new ArgumentException("Not on the menu: " + itemId);
            quantity = Math.Max(1, Math.Min(quantity, 9));
            var line = new CartLine { Item = menu.Id, Quantity = quantity };
            Lines.Add(line);
            return line;
        }
        public void Remove(CartLine line)
        {
            if (Editable) Lines.Remove(line);
        }
        public void ChangeQuantity(CartLine line, int quantity)
        {
            if (!Editable) return;
            if (quantity <= 0) Lines.Remove(line);
            else line.Quantity = Math.Min(quantity, 9);
        }
        /// The first line that still needs a question answered, if any.
        public CartLine NeedsDetail() => Lines.FirstOrDefault(line => line.Missing().Any());
        public bool Ready => !Empty && NeedsDetail() == null;
        public bool Confirm()
        {
            if (!Ready || !Editable) return false;
            Status = OrderStatus.Confirmed;
            return true;
        }
        public void Reopen()
        {
            if (Status == OrderStatus.Confirmed) Status = OrderStatus.Browsing;
        }
        /// Remaining servings are saved in the cart; the receipt keeps the original purchase intact.
        public CartLine ConsumeOne()
        {
            if (Status != OrderStatus.Delivered || Empty) return null;
            var serving = Lines[0];
            if (--serving.Quantity == 0) Lines.RemoveAt(0);
            if (Empty) Status = OrderStatus.Finished;
            return serving;
        }
        /// Each person's share. The first payer covers any leftover cent so the total always matches.
        public int[] Split(int payers)
        {
            payers = Math.Max(1, Math.Min(payers, 6));
            var share = Total / payers;
            var shares = Enumerable.Repeat(share, payers).ToArray();
            shares[0] += Total - share * payers;
            return shares;
        }
        public string Summary() => Empty ? "Your cart is empty." : string.Join("\n", Lines.Select(line => line.Describe() + " · " + CafeMenu.Money(line.Total)));
    }

    [Serializable]
    public sealed class Wallet
    {
        public int Cash = 3000, Card = 6000; // cents

        public int Balance => Cash + Card;
        /// Pays only the player's share; friends pay theirs when the bill is split.
        public bool TryPay(OrderCart cart, string method, int minute, out Receipt receipt, out string problem)
        {
            receipt = null;
            problem = "";
            if (cart.Status != OrderStatus.Confirmed) { problem = "Confirm the order before paying."; return false; }
            var share = cart.Split(cart.Payers)[0];
            if (method == "cash")
            {
                if (Cash < share) { problem = "You only have " + CafeMenu.Money(Cash) + " in cash."; return false; }
                Cash -= share;
            }
            else
            {
                method = "card";
                if (Card < share) { problem = "Your card was declined. Try cash or remove an item."; return false; }
                Card -= share;
            }
            receipt = new Receipt
            {
                Id = "FC-" + minute.ToString("0000") + "-" + cart.Lines.Count,
                Lines = cart.Lines.Select(line => line.Describe() + " · " + CafeMenu.Money(line.Total)).ToArray(),
                Total = cart.Total, Paid = share, Payers = cart.Payers, Method = method, GameMinute = minute,
            };
            cart.Status = OrderStatus.Paid;
            return true;
        }
    }
}

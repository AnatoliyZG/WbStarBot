using System;
namespace WbStarBot
{
	public class Transaction
	{
		public string orderId;
		public uint amount;
		public DateTime date;

		public Transaction(string orderId, uint amount)
		{
			this.orderId = orderId;
			this.amount = amount;
			this.date = DateTime.Now;

		}

        public override string ToString()
        {
            return $"📄 *{orderId}*\n💸 Сумма: {amount}р.\n📆 Дата: {date}";
        }
    }

	public class Pay
	{
		public DateTime date;

		public Pay()
		{
			date = DateTime.Now;
		}
        public override string ToString()
        {
            return $"📆 Дата: {date}\n💸 Сумма: {CONSTS.WeekCost}р.";
        }
    }
}


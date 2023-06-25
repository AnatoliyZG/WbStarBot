using System;
namespace WbStarBot.DataTypes
{
	public class ProductInfo
	{
        public string? name;
        public float? valuation;
        public int? feedbackCount;
        public short? fee;

        public SearchPosition searchPosition = new SearchPosition();

        public DateTime lastUpd;

        public ProductInfo()
        {
            lastUpd = DateTime.UtcNow;
        }

        public struct SearchPosition
        {
            public int? currentPosition
            {
                get => position;
                set
                {
                    if (value == null) return;

                    if(position != null)
                    {
                        positionUp = value >= position;
                    }

                    position = value;
                }
            }

            public int page => currentPosition == null ? 0 : (currentPosition.Value / 100 + 1);
            public int pagePositon => currentPosition == null ? 0 : (currentPosition.Value % 100 );

            public bool searchUpper => positionUp;

            private int? position;
            private bool positionUp = true;

            public SearchPosition()
            {

            }
        }
    }
}


﻿using System.Threading.Tasks;

namespace Tadmor.Services.Marriage
{
    [BabyFrequency(0.2)]
    public class SiameseBaby : Baby
    {
        public override string GetDescription()
        {
            return "when coupled, significantly increases kisses; otherwise, slightly decreases them";
        }

        public override Task Release(MarriedCouple marriage)
        {
            marriage.Kisses += Rank;
            return Task.CompletedTask;
        }
    }
}
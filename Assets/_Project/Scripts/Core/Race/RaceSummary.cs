namespace HighwayRenegade.Core.Race
{
    /// <summary>
    /// What a finished race was worth. The contract between the system that ends a race
    /// and anything that reports it.
    ///
    /// Why this type exists: the results screen used to compute its own prize money from
    /// its own hardcoded table and add it to the balance, while CampaignSession was
    /// separately awarding the event purse for the same race. Both ran, so a campaign
    /// race paid twice, by two formulas that did not agree - and the number shown to the
    /// player matched neither balance.
    ///
    /// Splitting the job fixes it by construction: whoever ends the race computes this
    /// once and applies it once, and the screen renders the result without touching the
    /// balance. A read-only value cannot pay anybody.
    /// </summary>
    public readonly struct RaceSummary
    {
        /// <summary>Finishing position, 1-based. Zero when the race was not completed.</summary>
        public readonly int Position;

        /// <summary>Rivals the player put down during the race.</summary>
        public readonly int Knockouts;

        /// <summary>Prize money for the finishing position, before deductions.</summary>
        public readonly int Prize;

        /// <summary>Bonus earned for knockouts.</summary>
        public readonly int CombatBonus;

        /// <summary>Cost to undo the damage taken this race.</summary>
        public readonly int RepairBill;

        /// <summary>Fine deducted if the police ended the race. Zero otherwise.</summary>
        public readonly int Fine;

        /// <summary>Balance after everything above was applied.</summary>
        public readonly int Balance;

        public RaceSummary(int position, int knockouts, int prize, int combatBonus,
                           int repairBill, int fine, int balance)
        {
            Position = position;
            Knockouts = knockouts;
            Prize = prize;
            CombatBonus = combatBonus;
            RepairBill = repairBill;
            Fine = fine;
            Balance = balance;
        }

        /// <summary>What the race actually added to, or took from, the balance.</summary>
        public int Net => Prize + CombatBonus - RepairBill - Fine;

        /// <summary>True when the player took the race outright.</summary>
        public bool IsWin => Position == 1;

        /// <summary>True once a race has genuinely finished and been costed.</summary>
        public bool IsValid => Position > 0;
    }
}

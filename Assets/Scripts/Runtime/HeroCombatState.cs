namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 35: small per-run, caster-scoped combat state owned by <c>HeroRuntime</c> and shared with the
    /// ability runtimes through the execution context. Currently it tracks Bolt Striker's Static Charge
    /// combo (consecutive hits on one target), which the basic builds and the Lethal Surge apex consumes —
    /// so the state has to live OUTSIDE any single <c>AbilityRuntime</c> instance. Generic enough to grow if
    /// other caster-side combat state appears later; not a static singleton (per CLAUDE.md §3.5).
    /// </summary>
    public sealed class HeroCombatState
    {
        /// <summary>The enemy the Static Charge combo is currently building on (null = none).</summary>
        public EnemyRuntime StaticChargeTarget { get; private set; }

        /// <summary>Accumulated consecutive-hit stacks on <see cref="StaticChargeTarget"/> (0 when fresh).</summary>
        public int StaticChargeStacks { get; private set; }

        /// <summary>
        /// Register a basic hit for Static Charge and return the stack count to use for THIS hit's bonus
        /// (apply-then-increment, so a fresh target's first hit gets no bonus and the bonus builds on
        /// consecutive hits). Switching targets resets the combo. <paramref name="maxStacks"/> caps the
        /// stored stacks; &lt; 1 means Static Charge isn't held, so the combo is cleared.
        /// </summary>
        public int RegisterBasicHit(EnemyRuntime target, int maxStacks)
        {
            if (maxStacks < 1 || target == null)
            {
                Reset();
                return 0;
            }

            int bonusStacks;
            if (target == StaticChargeTarget)
            {
                bonusStacks = StaticChargeStacks;                                  // bonus from prior consecutive hits
                if (StaticChargeStacks < maxStacks) StaticChargeStacks++;          // build toward the cap
            }
            else
            {
                StaticChargeTarget = target;                                       // switched target → combo resets
                bonusStacks = 0;
                StaticChargeStacks = 1;
            }
            return bonusStacks;
        }

        /// <summary>Read the current stacks and clear the combo (Lethal Surge consumes them on trigger).</summary>
        public int ConsumeStaticCharge()
        {
            int stacks = StaticChargeStacks;
            Reset();
            return stacks;
        }

        public void Reset()
        {
            StaticChargeTarget = null;
            StaticChargeStacks = 0;
        }
    }
}

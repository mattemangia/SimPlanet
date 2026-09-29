namespace SimPlanet;

/// <summary>
/// Animal migrations. Where wildlife is abundant, herds (land), flocks (air) and schools
/// (sea) form and travel with the seasons between summer ranges toward the poles and winter
/// ranges toward the equator. Herds graze the vegetation they cross; settlements along their
/// path hunt them for food, and heavy hunting can wipe a group out.
/// </summary>
public partial class CivilizationManager
{
    private int _nextAnimalGroupId = 1;
    private float _seasonPhase;                         // 0-1 through the year

    private const int MaxAnimalGroups = 40;

    #region Yearly: spawning, hunting, extinction

    private void UpdateWildlife(int currentYear)
    {
        // Hunting by settlements near a passing group
        foreach (var group in _animalGroups.ToList())
        {
            foreach (var civ in _civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    if (WrappedDistance(city.X, city.Y, (int)group.X, (int)group.Y) > 4) continue;
                    int hunted = (int)(group.Size * 0.04f * (group.Marine ? 1.5f : 1f) * (1f - civ.EcoFriendliness * 0.6f));
                    group.Size -= hunted;
                    civ.Food += hunted * 0.02f; // Meat and fish for the granaries
                }
            }
            if (group.Size < 20)
            {
                _animalGroups.Remove(group);
            }
        }

        // Natural growth of surviving groups
        foreach (var group in _animalGroups)
        {
            group.Size = Math.Min(group.Size + (int)(group.Size * 0.05f), group.Flying ? 20000 : 5000);
        }

        if (_animalGroups.Count >= MaxAnimalGroups) return;

        // New groups form where wildlife thrives
        for (int attempt = 0; attempt < 30 && _animalGroups.Count < MaxAnimalGroups; attempt++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height / 10, _map.Height * 9 / 10);
            var cell = _map.Cells[x, y];
            if (cell.Biomass < 0.4f) continue;

            var (species, name, flying, marine) = cell.LifeType switch
            {
                LifeForm.Mammals => (LifeForm.Mammals, "Herd", false, false),
                LifeForm.ComplexAnimals => (LifeForm.ComplexAnimals, "Herd", false, false),
                LifeForm.Dinosaurs => (LifeForm.Dinosaurs, "Saurian herd", false, false),
                LifeForm.Birds => (LifeForm.Birds, "Bird flock", true, false),
                LifeForm.Pterosaurs => (LifeForm.Pterosaurs, "Pterosaur flock", true, false),
                LifeForm.Fish => (LifeForm.Fish, "Fish school", false, true),
                LifeForm.MarineDinosaurs => (LifeForm.MarineDinosaurs, "Sea reptile pod", false, true),
                _ => (LifeForm.None, "", false, false)
            };
            if (species == LifeForm.None) continue;
            if (_animalGroups.Any(g => WrappedDistance((int)g.X, (int)g.Y, x, y) < 10)) continue;

            // Summer range toward the pole, winter range toward the equator
            int equator = _map.Height / 2;
            int span = (int)(_map.Height * (flying ? 0.25f : marine ? 0.15f : 0.1f));
            int poleward = y < equator ? -span : span;
            int homeY = Math.Clamp(y + poleward / 2, 2, _map.Height - 3);
            int winterY = Math.Clamp(y - poleward / 2, 2, _map.Height - 3);

            _animalGroups.Add(new AnimalGroup
            {
                Id = _nextAnimalGroupId++,
                Species = species,
                Name = name,
                X = x,
                Y = y,
                Size = flying ? 2000 + _random.Next(8000) : 200 + _random.Next(1500),
                Flying = flying,
                Marine = marine,
                HomeY = homeY,
                WinterY = winterY,
                TargetX = x,
                TargetY = y
            });
        }
    }

    #endregion

    #region Continuous: seasonal movement

    private void UpdateAnimalMovement(float deltaYears)
    {
        if (_animalGroups.Count == 0 || deltaYears <= 0) return;

        _seasonPhase = (_seasonPhase + deltaYears) % 1f;
        // Northern summer in the first half of the year; the southern hemisphere is reversed
        bool northernSummer = _seasonPhase < 0.5f;

        foreach (var group in _animalGroups)
        {
            bool northern = group.Y < _map.Height / 2;
            bool summer = northern ? northernSummer : !northernSummer;
            float targetY = summer ? group.HomeY : group.WinterY;

            // Wander east-west while following the season north-south
            if (Math.Abs(group.TargetY - targetY) > 0.5f || WrappedDistance((int)group.X, (int)group.Y, (int)group.TargetX, (int)group.TargetY) < 2)
            {
                group.TargetY = targetY;
                group.TargetX = (group.X + (float)(_random.NextDouble() - 0.5) * (group.Flying ? 30f : 12f) + _map.Width) % _map.Width;
            }

            float speed = (group.Flying ? 60f : group.Marine ? 30f : 18f) * deltaYears; // Cells per year
            speed = Math.Min(speed, 1.5f);

            float dx = group.TargetX - group.X;
            if (Math.Abs(dx) > _map.Width / 2f) dx -= Math.Sign(dx) * _map.Width;
            float dy = group.TargetY - group.Y;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            if (length < 0.01f) continue;

            float nx = group.X + dx / length * speed;
            float ny = Math.Clamp(group.Y + dy / length * speed, 1, _map.Height - 2);
            if (nx < 0) nx += _map.Width;
            if (nx >= _map.Width) nx -= _map.Width;

            // Land herds stay on land, sea life in the sea; birds fly anywhere
            var next = _map.Cells[(int)nx % _map.Width, (int)ny];
            if (!group.Flying && (group.Marine ? next.IsLand : next.IsWater || next.IsIce))
            {
                group.TargetX = (group.X + (float)(_random.NextDouble() - 0.5) * 20f + _map.Width) % _map.Width;
                continue;
            }

            group.X = nx;
            group.Y = ny;
            group.Heading = MathF.Atan2(dy, dx);

            // Grazing
            if (!group.Flying && !group.Marine && next.Biomass > 0.2f)
            {
                next.Biomass = Math.Max(0.15f, next.Biomass - 0.0005f * group.Size / 500f);
            }
        }
    }

    #endregion
}

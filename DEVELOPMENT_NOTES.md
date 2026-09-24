# Shot types

- Camera placement uses left, right, front, and rear positions only.
- Placement adds a random angle variation from -25 to +25 degrees.
- Overhead positions are excluded.

## Fixed shot

- The camera remains fixed in world space and continually aims at the player.
- For a stationary player, placement is selected randomly around the player.
- For a moving player, placement is left-front or right-front with a small lead.
- The player is treated as a moving obstacle; the camera moves aside and settles
  at a new fixed position if the player approaches it directly.
- The shot ends on a requested cut, maximum duration, distance outside the
  allowed range, or loss of player visibility.

## Mobile shot

- The camera begins left, right, in front of, or behind the player.
- It follows while preserving its initial distance and absolute world direction
  when viewed from above.
- Its position does not depend on the player's orientation or movement direction.
- It continually aims at the player.
- The shot ends on a requested cut, maximum duration, distance outside the
  allowed range, or loss of player visibility.

## Gameplay shot

- The normal player camera is used.
- The shot ends on a requested cut or maximum duration.

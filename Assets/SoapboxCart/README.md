# Test Soapbox Cart

Use **Tools > Downhill Carts > Create Test Soapbox Cart** to rebuild the primitive cart prefab.

## Controls

- **A / D:** steer and shift the rider's weight
- **S:** smoothly apply the brakes
- **W:** pump the cart; release and press repeatedly for additional pushes. Each valid pump briefly leans the rider forward.
- **C / gamepad right-stick click:** switch between the first-person driving view and smooth third-person chase camera

The cart is gravity driven. Place the `SoapboxCart` prefab slightly above a sloped collider, with its blue Z axis pointing downhill.

## Tuning

All handling values are exposed on `SoapboxCartController`. The main starting points are maximum steer angle, brake torque, pump impulse, maximum speed, and the crash thresholds. Damage progressively reduces steering authority. During normal driving the placeholder rider is a collider-free visual child of the cart, so it follows lean and chassis motion without interfering with vehicle physics. A fatal collision detaches it, enables its colliders and activates physics. The crash event remains available for connecting a later production rider. Reaching zero health makes the cart undriveable.

Keyboard steering is smoothed rather than applied as an instant full-lock input. The default 26-degree low-speed steering angle progressively falls to 13 degrees at 60 km/h. Input ramp time, return time, wheel rotation speed and both steering angles are exposed under **Steering and lean**.

Suspension spring, damping, travel and grounded downforce are configured on the controller. The defaults favour keeping the small wheels planted on uneven downhill terrain; raise the spring gradually if the chassis bottoms out, and raise damping gradually if oscillation returns.

Downhill acceleration is still gravity-driven, but `Downhill Gravity Multiplier` strengthens only the forward part of gravity acting along the contacted slope. A genuine descent also maintains at least 1.4 m/s² acceleration, preventing shallow downhill sections from producing an unintended speed plateau. The terminal-speed control remains 105 km/h. Setting the multiplier to 1 disables the extra downhill acceleration.

`CartCameraController` automatically uses the scene's camera tagged `MainCamera`, or creates one if none exists. Its first-person position, third-person offset, smoothing, field of view, and speed-based FOV increase are all adjustable in the Inspector.

At speed, the lower chase camera gradually widens its field of view, gains vibration and rolls into fast turns. First- and third-person views have separate FOV increases so the chase view can use stronger speed exaggeration without making first person uncomfortable. `CartSpeedEffects` adds motion blur, denser peripheral streaks, an asset-free procedural wind sound and a speedometer. These effects begin at 12 km/h and reach full strength at 95 km/h by default; every effect can be reduced or disabled in the Inspector.

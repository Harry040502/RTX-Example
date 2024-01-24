using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Numerics;

namespace RayTracingConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            int width = 800;
            int height = 600;
            Bitmap image = new Bitmap(width, height);

            // Adjust the positions of the spheres if necessary
            Sphere reflectiveSphere = new Sphere(new Vector3(width / 2, height / 2, 1000), 150, Color.Gray, true, 0.9f);
            Sphere coloredSphere = new Sphere(new Vector3(width / 2 + 150, height / 2, 1000), 100, Color.Red, false, 0.0f);
            Sphere[] spheres = { reflectiveSphere, coloredSphere };

            // Set up the camera to look at the center of the scene where the spheres are
            Vector3 cameraPosition = new Vector3(width / 2, height / 2, 0);
            Vector3 lookAtPosition = new Vector3(width / 2, height / 2, 500);
            Camera camera = new Camera(cameraPosition, lookAtPosition, Vector3.UnitY);
            Console.WriteLine($"Camera Position: {camera.Position}");
            foreach (var sphere in spheres)
            {
                Console.WriteLine($"Sphere Position: {sphere.Center}, Radius: {sphere.Radius}");
            }

            // Define light direction
            Vector3 lightDirection = Vector3.Normalize(new Vector3(-1, -1, -1));

            int hitCount = 0;

            // Render the scene
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Ray ray = camera.GetRayThroughPixel(x, y, width, height);
                    Color colour = TraceRay(ray, spheres, lightDirection, 3);
                    if ((x == 0 && y == 0) || (x == width - 1 && y == 0) || (x == 0 && y == height - 1) || (x == width - 1 && y == height - 1))
                    {
                        Console.WriteLine($"Ray direction at ({x}, {y}): {ray.Direction}");
                    }
                    
                    if (colour != Color.FromArgb(20, 20, 20))
                    {
                        hitCount++;
                    }
                    image.SetPixel(x, y, colour);
                }
            }

            // Save the rendered image
            image.Save("raytraced_scene.png");
            Console.WriteLine($"Ray tracing completed. Image saved as 'raytraced_scene.png'. Spheres hit: {hitCount} times");
        }

        static Color TraceRay(Ray ray, Sphere[] spheres, Vector3 lightDirection, int depth)
        {
            if (depth <= 0) return Color.FromArgb(10, 10, 10);

            Sphere closestSphere = null;
            float minDistance = float.MaxValue;
            Vector3 hitPoint = Vector3.Zero;
            bool hit = false;

            foreach (var sphere in spheres)
            {
                if (sphere.RayIntersects(ray, out var distance))
                {
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        hitPoint = ray.Origin + ray.Direction * distance;
                        closestSphere = sphere;
                        hit = true;
                    }
                }
            }
            Color ambientColor = Color.FromArgb(20, 20, 20);
            if (hit)
            {
                Vector3 normal = Vector3.Normalize(hitPoint - closestSphere.Center);
                Vector3 lightDir = Vector3.Normalize(lightDirection);
                float diffIntensity = Math.Max(Vector3.Dot(normal, lightDir), 0.0f);
                
                Color diffuseColor = ScaleColor(closestSphere.Color, diffIntensity);

                if (closestSphere.Reflective)
                {
                    Vector3 offsetHitPoint = hitPoint + 0.001f * normal; // Small offset to the hit point to avoid self-intersection
                    Vector3 reflectDir = Vector3.Reflect(ray.Direction, normal);
                    Ray reflectRay = new Ray(offsetHitPoint, reflectDir);
                    Color reflectColor = TraceRay(reflectRay, spheres, lightDirection, depth - 1);
                    return BlendColors(diffuseColor, reflectColor, closestSphere.Reflectivity);
                }
                else
                {
                    return AddColors(diffuseColor, ambientColor);
                }
            }
            else
            {
                return ambientColor; // Environment color
            }
        }

        static Color BlendColors(Color baseColor, Color reflectColor, float reflectivity)
        {
            // Blend base colour with reflection based on reflectivity
            float r = baseColor.R * (1 - reflectivity) + reflectColor.R * reflectivity;
            float g = baseColor.G * (1 - reflectivity) + reflectColor.G * reflectivity;
            float b = baseColor.B * (1 - reflectivity) + reflectColor.B * reflectivity;
            return Color.FromArgb(ClampColor(r), ClampColor(g), ClampColor(b));
        }

        static Color AddColors(Color c1, Color c2)
        {
            // Add two colors together
            int r = ClampColor(c1.R + c2.R);
            int g = ClampColor(c1.G + c2.G);
            int b = ClampColor(c1.B + c2.B);
            return Color.FromArgb(255, r, g, b);
        }

        static Color ScaleColor(Color colour, float scale)
        {
            // Scale a colour by a given factor
            return Color.FromArgb(255, ClampColor(colour.R * scale), ClampColor(colour.G * scale), ClampColor(colour.B * scale));
        }

        static int ClampColor(float value)
        {
            // Clamp colour values to valid range
            return (int)Math.Max(Math.Min(value, 255), 0);
        }
    }

    class Camera
    {
        public Vector3 Position { get; }
        public Vector3 Forward { get; private set; }
        public Vector3 Up { get; private set; }

        public Camera(Vector3 position, Vector3 lookAt, Vector3 up)
        {
            Position = position;
            Forward = Vector3.Normalize(lookAt - position);
            Up = Vector3.Normalize(Vector3.Cross(Vector3.Normalize(Vector3.Cross(up, Forward)), Forward));
        }

        public Ray GetRayThroughPixel(int x, int y, int imageWidth, int imageHeight)
        {
            float aspectRatio = (float)imageWidth / imageHeight;
            float fovRadians = MathF.PI / 4; // 45 degrees field of view in radians
            float scale = MathF.Tan(fovRadians / 2);

            float pixelNDCX = (x + 0.5f) / imageWidth * 2 - 1;
            float pixelNDCY = (y + 0.5f) / imageHeight * 2 - 1;
            Vector3 pixelCameraSpace = new Vector3(pixelNDCX * scale * aspectRatio, -pixelNDCY * scale, 1);

            Vector3 right = Vector3.Normalize(Vector3.Cross(Up, Forward));
            Vector3 rayDirection = Vector3.Normalize(pixelCameraSpace.X * right + pixelCameraSpace.Y * Up + pixelCameraSpace.Z * Forward);
            return new Ray(Position, rayDirection);
        }
    }

    class Sphere
    {
        public Vector3 Center { get; }
        public float Radius { get; }
        public Color Color { get; }
        public bool Reflective { get; }
        public float Reflectivity { get; }

        public Sphere(Vector3 center, float radius, Color colour, bool reflective, float reflectivity)
        {
            Center = center;
            Radius = radius;
            Color = colour;
            Reflective = reflective;
            Reflectivity = reflectivity;
        }

        public bool RayIntersects(Ray ray, out float t)
        {
            t = 0;
            Vector3 oc = ray.Origin - Center;
            float a = Vector3.Dot(ray.Direction, ray.Direction);
            float b = 2.0f * Vector3.Dot(oc, ray.Direction);
            float c = Vector3.Dot(oc, oc) - Radius * Radius;
            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
            {
                return false; // No intersection
            }

            float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
            float t0 = (-b - sqrtDiscriminant) / (2 * a);
            float t1 = (-b + sqrtDiscriminant) / (2 * a);

            if (t0 > 0 && t1 > 0)
            {
                t = Math.Min(t0, t1);
            }
            else if (t0 > 0)
            {
                t = t0;
            }
            else if (t1 > 0)
            {
                t = t1;
            }
            else
            {
                return false; // Both intersection points are behind the ray origin
            }
            return true;
        }
    }

    class Ray
    {
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = Vector3.Normalize(direction);
        }
    }
}

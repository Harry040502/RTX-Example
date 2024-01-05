using System;
using System.Drawing;
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
            Vector3 cameraPosition = new Vector3(width / 2, height / 2, -1000); // Centered on X and Y axis
            Vector3 lookAtPosition = new Vector3(width / 2, height / 2, 1000); // Look at the center of where the spheres are placed
            Camera camera = new Camera(cameraPosition, lookAtPosition, Vector3.UnitY);


            // Define light direction
            Vector3 lightDirection = Vector3.Normalize(new Vector3(-1, -1, -1));

            // Render the scene
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Ray ray = camera.GetRayThroughPixel(x, y, width, height);
                    Color colour = TraceRay(ray, spheres, lightDirection, 3);
                    image.SetPixel(x, y, colour);
                }
            }

            // Save the rendered image
            image.Save("raytraced_scene.png");
            Console.WriteLine("Ray tracing completed. Image saved as 'raytraced_scene.png'");
        }

        static Color TraceRay(Ray ray, Sphere[] spheres, Vector3 lightDirection, int depth)
        {
            // Base case for recursion
            if (depth <= 0) return Color.FromArgb(10, 10, 10);

            Sphere closestSphere = null;
            float minDistance = float.MaxValue;
            Vector3 hitPoint = Vector3.Zero;
            Vector3 normalAtHitPoint = Vector3.Zero;
            bool hit = false;

            // Find the closest sphere intersection
            foreach (var sphere in spheres)
            {
                if (sphere.RayIntersects(ray, out var distance) && distance < minDistance)
                {
                    minDistance = distance;
                    hitPoint = ray.Origin + ray.Direction * distance;
                    normalAtHitPoint = Vector3.Normalize(hitPoint - sphere.Center);
                    closestSphere = sphere;
                    hit = true;
                }
            }

            // Calculate colour based on intersection
            if (hit && closestSphere != null)
            {
                float diffuse = Math.Max(Vector3.Dot(normalAtHitPoint, -Vector3.Normalize(lightDirection)), 0);
                Color diffuseColor = ScaleColor(closestSphere.Color, diffuse);
                Color ambientColor = ScaleColor(closestSphere.Color, 0.3f); // Ambient light

                if (closestSphere.Reflective)
                {
                    Vector3 reflectDir = Vector3.Reflect(ray.Direction, normalAtHitPoint);
                    Ray reflectRay = new Ray(hitPoint + reflectDir * 0.001f, reflectDir);
                    Color reflectColor = TraceRay(reflectRay, spheres, lightDirection, depth - 1);
                    return BlendColors(diffuseColor, reflectColor, closestSphere.Reflectivity);
                }
                return AddColors(diffuseColor, ambientColor);
            }

            // Environment colour for rays that don't hit anything
            return Color.FromArgb(20, 20, 20);
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
            float fov = 90.0f; // Field of view in degrees
            float angle = (float)Math.Tan(Math.PI * 0.5 * fov / 180.0);

            // Map the pixel coordinate to [-1, 1] range (left to right and bottom to top)
            float px = (2 * ((x + 0.5f) / imageWidth) - 1) * angle * aspectRatio;
            float py = (1 - 2 * ((y + 0.5f) / imageHeight)) * angle;

            Vector3 imagePoint = new Vector3(px, py, 1); // Set forward to 1 for the forward direction
            Vector3 direction = Vector3.Normalize(Forward + imagePoint - Position);

            return new Ray(Position, direction);
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
                return false;
            }

            float t0 = (-b - (float)Math.Sqrt(discriminant)) / (2.0f * a);
            float t1 = (-b + (float)Math.Sqrt(discriminant)) / (2.0f * a);

            if (t0 < 0 && t1 < 0) return false;

            t = Math.Min(t0, t1);
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

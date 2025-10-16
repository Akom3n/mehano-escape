using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace DoomRaycastWinForms
{
    public partial class Form1 : Form
    {
        // Map & world
        const int mapWidth = 10;
        const int mapHeight = 10;
        const int blockSize = 64;
        int[,] map =
        {
            {1,1,1,1,1,1,1,1,1,1},
            {1,0,0,0,0,0,0,0,0,1},
            {1,0,1,0,1,0,1,0,0,1},
            {1,0,1,0,1,0,1,0,0,1},
            {1,0,0,0,0,0,1,0,0,1},
            {1,0,1,1,1,0,1,1,0,1},
            {1,0,0,0,1,0,0,0,0,1},
            {1,0,1,0,1,1,1,1,0,1},
            {1,0,0,0,0,0,0,0,0,1},
            {1,1,1,1,1,1,1,1,1,1}
        };

        // Player / camera
        float playerX = 300;
        float playerY = 300;
        float playerAngle = 0;
        float fov = (float)(Math.PI / 3); // 60°
        float moveSpeed = 3f;
        float rotSpeed = 0.05f;

        bool forward, backward, turnLeft, turnRight;
        private SoundPlayer gunshotSound;
        // Gun & ammo
        Gun currentGun;

        // Bullets in flight
        List<Bullet> bullets = new List<Bullet>();

        // Bullet holes (position in world + which wall cell, and offset)
        List<BulletHole> bulletHoles = new List<BulletHole>();

        // Ammo boxes
        List<AmmoBox> ammoBoxes = new List<AmmoBox>();
        Random rand = new Random();

        // Timer
        System.Windows.Forms.Timer gameTimer = new System.Windows.Forms.Timer();

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.Width = 800;
            this.Height = 600;
            this.KeyPreview = true;
            this.KeyDown += OnKeyDown;
            this.KeyUp += OnKeyUp;
            this.MouseDown += OnMouseDown;

            // Start with USP‑S: small pistol, 2 projectiles per shot (for example), 3 clips
            currentGun = new Gun("USP‑S", 2, 15, 3);
            // 2 = number of projectiles per shot, 15 bullets per clip, 3 clips

            // Initialize the PictureBox field here (NOT in InitializeComponent)
            pictureBoxGun = new PictureBox();

            // Load your gun image - adjust path as needed
            string imagePath = @"C:\Users\students\source\repos\Akom3n\mehano-escape\DoomRaycastWinForms\bin\Debug\net8.0-windows\lib\usp-s.png";
            pictureBoxGun.Image = Image.FromFile(imagePath);

            pictureBoxGun.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxGun.Width = 200;
            pictureBoxGun.Height = 100;

            // Position it bottom center
            pictureBoxGun.Top = this.ClientSize.Height - pictureBoxGun.Height - 10;
            pictureBoxGun.Left = (this.ClientSize.Width - pictureBoxGun.Width) / 2;
            pictureBoxGun.BackColor = Color.Transparent;

            this.Controls.Add(pictureBoxGun);
            pictureBoxGun.BringToFront();

            this.Resize += Form1_Resize;

            gunshotSound = new SoundPlayer(@"C:\Users\students\source\repos\Akom3n\mehano-escape\DoomRaycastWinForms\lib\dartgun.wav-c3411f381358dd8992015e0856782c4a.wav"); // Adjust path to your wav file
            try
            {
                gunshotSound.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load gunshot sound: " + ex.Message);
            }

            // Start timer
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (pictureBoxGun != null)
            {
                pictureBoxGun.Top = this.ClientSize.Height - pictureBoxGun.Height - 10;
                pictureBoxGun.Left = (this.ClientSize.Width - pictureBoxGun.Width) / 2;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) forward = true;
            if (e.KeyCode == Keys.S) backward = true;
            if (e.KeyCode == Keys.A) turnLeft = true;
            if (e.KeyCode == Keys.D) turnRight = true;
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) forward = false;
            if (e.KeyCode == Keys.S) backward = false;
            if (e.KeyCode == Keys.A) turnLeft = false;
            if (e.KeyCode == Keys.D) turnRight = false;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShootCurrentGun();
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            UpdatePlayer();
            UpdateBullets();
            UpdateAmmoBoxes();
            Invalidate();
        }

        private void ShootCurrentGun()
        {
            if (!currentGun.HasAmmo()) return;

            currentGun.Shoot();

            // For each projectile
            for (int p = 0; p < currentGun.ProjectilesPerShot; p++)
            {
                // Random slight spread
                float spread = (float)((rand.NextDouble() - 0.5) * 0.1); // ±0.05 radians
                float shotAngle = playerAngle + spread;

                Bullet b = new Bullet
                {
                    x = playerX,
                    y = playerY,
                    dirX = (float)Math.Cos(shotAngle),
                    dirY = (float)Math.Sin(shotAngle),
                    speed = 10f
                };
                bullets.Add(b);
            }
        }

        private void UpdatePlayer()
        {
            float dx = (float)Math.Cos(playerAngle);
            float dy = (float)Math.Sin(playerAngle);

            if (forward)
            {
                float nx = playerX + dx * moveSpeed;
                float ny = playerY + dy * moveSpeed;
                if (!IsWall(nx, ny)) { playerX = nx; playerY = ny; }
            }
            if (backward)
            {
                float nx = playerX - dx * moveSpeed;
                float ny = playerY - dy * moveSpeed;
                if (!IsWall(nx, ny)) { playerX = nx; playerY = ny; }
            }
            if (turnLeft) playerAngle -= rotSpeed;
            if (turnRight) playerAngle += rotSpeed;
        }

        private void UpdateBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].Move();

                // If bullet out of range, remove
                if (bullets[i].DistanceTravelled > 1000)
                {
                    bullets.RemoveAt(i);
                    continue;
                }

                // Check collision with wall
                if (IsWall(bullets[i].x, bullets[i].y))
                {
                    // Determine which map cell, and get offset within cell to mark bullet hole
                    int mapX = (int)(bullets[i].x / blockSize);
                    int mapY = (int)(bullets[i].y / blockSize);

                    float offsetX = (bullets[i].x % blockSize) / blockSize;
                    float offsetY = (bullets[i].y % blockSize) / blockSize;

                    BulletHole hole = new BulletHole
                    {
                        mapX = mapX,
                        mapY = mapY,
                        offsetX = offsetX,
                        offsetY = offsetY
                    };

                    bulletHoles.Add(hole);
                    bullets.RemoveAt(i);
                    continue;
                }

                // TODO: Check collision with enemies (if any)
            }
        }

        private void UpdateAmmoBoxes()
        {
            // Periodically spawn ammo boxes (very simple)
            if (rand.NextDouble() < 0.005) // small chance each frame
            {
                // Try position that is not wall
                int cellX = rand.Next(1, mapWidth - 1);
                int cellY = rand.Next(1, mapHeight - 1);
                if (map[cellY, cellX] == 0)
                {
                    AmmoBox box = new AmmoBox
                    {
                        mapX = cellX,
                        mapY = cellY,
                        ammoAmount = 15
                    };
                    ammoBoxes.Add(box);
                }
            }

            // Player picking up boxes
            for (int i = ammoBoxes.Count - 1; i >= 0; i--)
            {
                float bx = ammoBoxes[i].mapX * blockSize + blockSize / 2;
                float by = ammoBoxes[i].mapY * blockSize + blockSize / 2;
                float dist = Distance(playerX, playerY, bx, by);
                if (dist < blockSize / 2)
                {
                    // Pickup
                    currentGun.AddClips(1); // or some logic
                    ammoBoxes.RemoveAt(i);
                }
            }
        }

        private float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private bool IsWall(float x, float y)
        {
            int mapX = (int)(x / blockSize);
            int mapY = (int)(y / blockSize);
            if (mapX < 0 || mapY < 0 || mapX >= mapWidth || mapY >= mapHeight)
                return true;
            return map[mapY, mapX] != 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);

            int screenWidth = this.ClientSize.Width;
            int screenHeight = this.ClientSize.Height;

            // Render 3D walls
            for (int x = 0; x < screenWidth; x++)
            {
                float rayAngle = playerAngle - fov / 2 + fov * x / screenWidth;

                float rayX = (float)Math.Cos(rayAngle);
                float rayY = (float)Math.Sin(rayAngle);

                float distance = 0;
                bool hit = false;
                float hitX = 0, hitY = 0;

                while (!hit && distance < 1000)
                {
                    distance += 1f;
                    float testX = playerX + rayX * distance;
                    float testY = playerY + rayY * distance;
                    if (IsWall(testX, testY))
                    {
                        hit = true;
                        hitX = testX;
                        hitY = testY;
                    }
                }

                // Remove fish-eye
                float correctedDist = distance * (float)Math.Cos(rayAngle - playerAngle);
                float wallHeight = (blockSize * screenHeight) / correctedDist;

                float startY = (screenHeight / 2) - (wallHeight / 2);
                float endY = (screenHeight / 2) + (wallHeight / 2);

                // Draw the wall slice
                int shade = 255 - (int)Math.Min(255, correctedDist);
                Color wallColor = Color.FromArgb(shade, shade, shade);
                g.DrawLine(new Pen(wallColor), x, (int)startY, x, (int)endY);

                // Also draw bullet holes that lie on that ray’s wall (approx)
                // Very simplified: check all bullet holes in same map cell
                int mapX = (int)(hitX / blockSize);
                int mapY = (int)(hitY / blockSize);

                foreach (var hole in bulletHoles)
                {
                    if (hole.mapX == mapX && hole.mapY == mapY)
                    {
                        // Convert hole.offsetX/offsetY to screen x position approx
                        float holeScreenX = x; // simplified, exactly this ray
                        float holeOffY = hole.offsetY; // use Y offset
                        // place a small dot on the wall slice
                        int hy = (int)(startY + holeOffY * wallHeight);
                        g.FillEllipse(Brushes.Black, holeScreenX - 1, hy - 1, 2, 2);
                    }
                }
            }

            // Draw mini map
            DrawMiniMap(g);

            // Optionally show ammo / gun info
            g.DrawString($"Gun: {currentGun.Name}", this.Font, Brushes.White, 10, 10);
            g.DrawString($"ClipAmmo: {currentGun.CurrentAmmo} / {currentGun.ClipSize}", this.Font, Brushes.White, 10, 30);
            g.DrawString($"Clips: {currentGun.Clips}", this.Font, Brushes.White, 10, 50);
        }

        private void DrawMiniMap(Graphics g)
        {
            int scale = 4;
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    Color c = map[y, x] == 1 ? Color.DarkGray : Color.LightGray;
                    g.FillRectangle(new SolidBrush(c), x * blockSize / scale, y * blockSize / scale, blockSize / scale, blockSize / scale);
                }
            }
            g.FillEllipse(Brushes.Red, playerX / scale - 2, playerY / scale - 2, 4, 4);
            float dirX = (float)Math.Cos(playerAngle);
            float dirY = (float)Math.Sin(playerAngle);
            g.DrawLine(Pens.Red, playerX / scale, playerY / scale, (playerX + dirX * 20) / scale, (playerY + dirY * 20) / scale);

            // draw ammo boxes
            foreach (var box in ammoBoxes)
            {
                Color c = Color.Green;
                g.FillRectangle(Brushes.Green, box.mapX * blockSize / scale + 2, box.mapY * blockSize / scale + 2, blockSize / scale - 4, blockSize / scale - 4);
            }
        }

        // Helper classes

        class Gun
        {
            public string Name;
            public int ProjectilesPerShot;
            public int ClipSize;
            public int Clips;
            public int CurrentAmmo;

            public Gun(string name, int projectilesPerShot, int clipSize, int clips)
            {
                Name = name;
                ProjectilesPerShot = projectilesPerShot;
                ClipSize = clipSize;
                Clips = clips;
                CurrentAmmo = clipSize;
            }

            public bool HasAmmo()
            {
                return CurrentAmmo > 0;
            }

            public void Shoot()
            {
                if (CurrentAmmo > 0)
                    CurrentAmmo--;
                else if (Clips > 0)
                {
                    // reload automatically
                    Clips--;
                    CurrentAmmo = ClipSize - 1;
                }
            }

            public void AddClips(int n)
            {
                Clips += n;
            }
        }

        class Bullet
        {
            public float x, y;
            public float dirX, dirY;
            public float speed;
            public float DistanceTravelled;

            public void Move()
            {
                x += dirX * speed;
                y += dirY * speed;
                DistanceTravelled += speed;
            }
        }

        class BulletHole
        {
            public int mapX, mapY;
            public float offsetX, offsetY;
        }

        class AmmoBox
        {
            public int mapX, mapY;
            public int ammoAmount;




            Bitmap gunSpriteSheet;
            int frameWidth;
            int frameHeight;
            int currentFrame = 0;
            int totalFrames = 5; // Set to how many frames your sprite sheet has per animation
            int frameTimer = 0;       }

         
           

        }

    }




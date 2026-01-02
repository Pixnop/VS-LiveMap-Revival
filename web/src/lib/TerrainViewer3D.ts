import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

import type { LiveMap } from '../LiveMap';

/**
 * Interface for 3D region data loaded from JSON files
 */
interface Region3DData {
	x: number;
	z: number;
	heightmap: number[];
	colors: number[];
	blockIds: number[];
	version: number;
	minY: number;
	maxY: number;
}

/**
 * Three.js based 3D terrain viewer for LiveMap
 */
export class TerrainViewer3D {
	private readonly _livemap: LiveMap;
	private readonly _container: HTMLDivElement;

	private _scene: THREE.Scene;
	private _camera: THREE.PerspectiveCamera;
	private _renderer: THREE.WebGLRenderer;
	private _controls: OrbitControls;

	private _isVisible: boolean = false;
	private _animationId: number | null = null;
	private _loadedRegions: Map<string, THREE.Mesh> = new Map();

	// Region size in blocks
	private static readonly REGION_SIZE = 512;
	// Block size for voxel rendering (sample every N blocks for performance)
	private static readonly BLOCK_SAMPLE = 2;
	// Scale factor for rendering
	private static readonly BLOCK_SCALE = 1;

	constructor(livemap: LiveMap) {
		this._livemap = livemap;

		// Create container element
		this._container = document.createElement('div');
		this._container.id = 'terrain-3d';
		this._container.style.cssText = `
			position: absolute;
			top: 0;
			left: 0;
			width: 100%;
			height: 100%;
			z-index: 500;
			display: none;
			background: linear-gradient(to bottom, #87CEEB 0%, #E0F6FF 100%);
		`;
		document.body.appendChild(this._container);

		// Initialize Three.js
		this._scene = new THREE.Scene();
		this._scene.background = new THREE.Color(0x87CEEB);

		// Setup camera
		this._camera = new THREE.PerspectiveCamera(
			60,
			window.innerWidth / window.innerHeight,
			1,
			50000,
		);
		this._camera.position.set(0, 500, 500);

		// Setup renderer
		this._renderer = new THREE.WebGLRenderer({ antialias: true });
		this._renderer.setSize(window.innerWidth, window.innerHeight);
		this._renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
		this._container.appendChild(this._renderer.domElement);

		// Setup controls
		this._controls = new OrbitControls(this._camera, this._renderer.domElement);
		this._controls.enableDamping = true;
		this._controls.dampingFactor = 0.05;
		this._controls.maxPolarAngle = Math.PI / 2 - 0.1;
		this._controls.minDistance = 50;
		this._controls.maxDistance = 10000;

		// Add lighting
		this.setupLighting();

		// Handle window resize
		window.addEventListener('resize', () => this.onResize());
	}

	private setupLighting(): void {
		// Ambient light for general illumination
		const ambient = new THREE.AmbientLight(0xffffff, 0.6);
		this._scene.add(ambient);

		// Directional light for shadows and depth
		const directional = new THREE.DirectionalLight(0xffffff, 0.8);
		directional.position.set(100, 200, 100);
		directional.castShadow = false;
		this._scene.add(directional);

		// Hemisphere light for sky/ground color variation
		const hemisphere = new THREE.HemisphereLight(0x87CEEB, 0x8B7355, 0.3);
		this._scene.add(hemisphere);
	}

	/**
	 * Show the 3D view
	 */
	public show(): void {
		if (this._isVisible) return;

		this._isVisible = true;
		this._container.style.display = 'block';

		// Hide the 2D map
		const mapContainer = this._livemap.getContainer();
		mapContainer.style.display = 'none';

		// Start render loop
		this.animate();

		// Load visible regions
		this.loadVisibleRegions();
	}

	/**
	 * Hide the 3D view
	 */
	public hide(): void {
		if (!this._isVisible) return;

		this._isVisible = false;
		this._container.style.display = 'none';

		// Show the 2D map
		const mapContainer = this._livemap.getContainer();
		mapContainer.style.display = 'block';

		// Stop render loop
		if (this._animationId !== null) {
			cancelAnimationFrame(this._animationId);
			this._animationId = null;
		}
	}

	/**
	 * Toggle between 2D and 3D view
	 */
	public toggle(): void {
		if (this._isVisible) {
			this.hide();
		} else {
			this.show();
		}
	}

	public get isVisible(): boolean {
		return this._isVisible;
	}

	private animate(): void {
		if (!this._isVisible) return;

		this._animationId = requestAnimationFrame(() => this.animate());
		this._controls.update();
		this._renderer.render(this._scene, this._camera);
	}

	private onResize(): void {
		if (!this._isVisible) return;

		this._camera.aspect = window.innerWidth / window.innerHeight;
		this._camera.updateProjectionMatrix();
		this._renderer.setSize(window.innerWidth, window.innerHeight);
	}

	/**
	 * Load visible regions based on spawn point
	 */
	private async loadVisibleRegions(): Promise<void> {
		const spawn = this._livemap.settings.spawn;

		// Calculate region coordinates from spawn
		const regionX = Math.floor(spawn.x / TerrainViewer3D.REGION_SIZE);
		const regionZ = Math.floor(spawn.z / TerrainViewer3D.REGION_SIZE);

		// Load a 3x3 grid of regions around spawn
		for (let dx = -1; dx <= 1; dx++) {
			for (let dz = -1; dz <= 1; dz++) {
				await this.loadRegion(regionX + dx, regionZ + dz);
			}
		}

		// Center camera on loaded terrain
		this.centerCameraOnSpawn();
	}

	/**
	 * Load a single region from the server
	 */
	private async loadRegion(regionX: number, regionZ: number): Promise<void> {
		const key = `${regionX}_${regionZ}`;

		// Skip if already loaded
		if (this._loadedRegions.has(key)) return;

		try {
			const url = `data/3d/region_${regionX}_${regionZ}.json`;
			const response = await fetch(url);

			if (!response.ok) {
				console.debug(`Region ${key} not found`);
				return;
			}

			const data: Region3DData = await response.json();
			const mesh = this.createTerrainMesh(data);

			this._loadedRegions.set(key, mesh);
			this._scene.add(mesh);

			console.debug(`Loaded region ${key}`);
		} catch (err) {
			console.error(`Error loading region ${key}:`, err);
		}
	}

	/**
	 * Create voxel-style terrain mesh from region data
	 */
	private createTerrainMesh(data: Region3DData): THREE.Mesh {
		const regionSize = TerrainViewer3D.REGION_SIZE;
		const sample = TerrainViewer3D.BLOCK_SAMPLE;
		const blockScale = TerrainViewer3D.BLOCK_SCALE;
		const gridSize = regionSize / sample;

		// Arrays for building geometry
		const positions: number[] = [];
		const colors: number[] = [];
		const normals: number[] = [];

		// Calculate base Y for height offset
		const baseY = data.minY;

		// Helper to get height at a position
		const getHeight = (x: number, z: number): number => {
			if (x < 0 || x >= regionSize || z < 0 || z >= regionSize) {
				return baseY;
			}
			const idx = z * regionSize + x;
			return data.heightmap[idx] ?? baseY;
		};

		// Helper to get color at a position (format is ARGB)
		const getColor = (x: number, z: number): [number, number, number] => {
			const idx = z * regionSize + x;
			const packed = data.colors[idx] ?? 0xFF808080;
			return [
				((packed >> 16) & 0xFF) / 255, // R
				((packed >> 8) & 0xFF) / 255,  // G
				(packed & 0xFF) / 255,          // B
			];
		};

		// Helper to add a face (quad as two triangles)
		const addFace = (
			v0: number[], v1: number[], v2: number[], v3: number[],
			normal: number[],
			color: [number, number, number],
		): void => {
			// Triangle 1: v0, v1, v2
			positions.push(...v0, ...v1, ...v2);
			// Triangle 2: v0, v2, v3
			positions.push(...v0, ...v2, ...v3);

			// Normals for 6 vertices
			for (let i = 0; i < 6; i++) {
				normals.push(...normal);
				colors.push(...color);
			}
		};

		// Iterate over sampled grid
		for (let gz = 0; gz < gridSize; gz++) {
			for (let gx = 0; gx < gridSize; gx++) {
				const worldX = gx * sample;
				const worldZ = gz * sample;

				// Get height at center of this block
				const height = getHeight(worldX + sample / 2, worldZ + sample / 2);
				const y = (height - baseY) * blockScale;
				const blockHeight = sample * blockScale;

				// Block corners in local space
				const x0 = gx * sample * blockScale;
				const x1 = x0 + sample * blockScale;
				const z0 = gz * sample * blockScale;
				const z1 = z0 + sample * blockScale;
				const y0 = 0; // Bottom at base
				const y1 = y; // Top at height

				// Get color for this block
				const color = getColor(worldX + sample / 2, worldZ + sample / 2);

				// Top face (always visible)
				addFace(
					[x0, y1, z0], [x0, y1, z1], [x1, y1, z1], [x1, y1, z0],
					[0, 1, 0],
					color,
				);

				// Check neighbors for side faces
				const heightN = getHeight(worldX + sample / 2, worldZ - sample + sample / 2);
				const heightS = getHeight(worldX + sample / 2, worldZ + sample + sample / 2);
				const heightW = getHeight(worldX - sample + sample / 2, worldZ + sample / 2);
				const heightE = getHeight(worldX + sample + sample / 2, worldZ + sample / 2);

				// Shade sides slightly darker
				const sideColor: [number, number, number] = [color[0] * 0.8, color[1] * 0.8, color[2] * 0.8];

				// North face (z-)
				if (height > heightN) {
					const ny = (heightN - baseY) * blockScale;
					addFace(
						[x1, y1, z0], [x1, ny, z0], [x0, ny, z0], [x0, y1, z0],
						[0, 0, -1],
						sideColor,
					);
				}

				// South face (z+)
				if (height > heightS) {
					const sy = (heightS - baseY) * blockScale;
					addFace(
						[x0, y1, z1], [x0, sy, z1], [x1, sy, z1], [x1, y1, z1],
						[0, 0, 1],
						sideColor,
					);
				}

				// West face (x-)
				if (height > heightW) {
					const wy = (heightW - baseY) * blockScale;
					addFace(
						[x0, y1, z0], [x0, wy, z0], [x0, wy, z1], [x0, y1, z1],
						[-1, 0, 0],
						sideColor,
					);
				}

				// East face (x+)
				if (height > heightE) {
					const ey = (heightE - baseY) * blockScale;
					addFace(
						[x1, y1, z1], [x1, ey, z1], [x1, ey, z0], [x1, y1, z0],
						[1, 0, 0],
						sideColor,
					);
				}
			}
		}

		// Create BufferGeometry
		const geometry = new THREE.BufferGeometry();
		geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
		geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
		geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));

		// Create material
		const material = new THREE.MeshLambertMaterial({
			vertexColors: true,
			side: THREE.FrontSide,
		});

		// Create mesh and position it
		const mesh = new THREE.Mesh(geometry, material);

		// Position based on region coordinates relative to spawn
		const spawn = this._livemap.settings.spawn;
		const worldX = data.x * regionSize - spawn.x;
		const worldZ = data.z * regionSize - spawn.z;

		mesh.position.set(worldX, 0, worldZ);

		return mesh;
	}

	/**
	 * Center camera on spawn point
	 */
	private centerCameraOnSpawn(): void {
		this._camera.position.set(0, 300, 400);
		this._controls.target.set(0, 0, 0);
		this._controls.update();
	}

	/**
	 * Dispose of all resources
	 */
	public dispose(): void {
		this.hide();

		// Dispose loaded meshes
		this._loadedRegions.forEach((mesh) => {
			mesh.geometry.dispose();
			if (mesh.material instanceof THREE.Material) {
				mesh.material.dispose();
			}
			this._scene.remove(mesh);
		});
		this._loadedRegions.clear();

		// Dispose renderer
		this._renderer.dispose();

		// Remove container
		this._container.remove();
	}
}

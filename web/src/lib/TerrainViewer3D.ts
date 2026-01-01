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
	// Scale factor for height visualization
	private static readonly HEIGHT_SCALE = 0.5;
	// Scale factor for XZ plane
	private static readonly XZ_SCALE = 1;

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
	 * Create terrain mesh from region data
	 */
	private createTerrainMesh(data: Region3DData): THREE.Mesh {
		const size = TerrainViewer3D.REGION_SIZE;
		const segments = 128; // Reduce resolution for performance
		const step = size / segments;

		// Create geometry
		const geometry = new THREE.PlaneGeometry(
			size * TerrainViewer3D.XZ_SCALE,
			size * TerrainViewer3D.XZ_SCALE,
			segments,
			segments,
		);

		// Rotate to be horizontal (XZ plane)
		geometry.rotateX(-Math.PI / 2);

		// Get position attribute
		const position = geometry.getAttribute('position');
		const colors: number[] = [];

		// Calculate base Y (sea level) for height offset
		const baseY = (data.minY + data.maxY) / 2;

		// Apply heightmap and colors
		for (let i = 0; i <= segments; i++) {
			for (let j = 0; j <= segments; j++) {
				const vertexIndex = i * (segments + 1) + j;

				// Sample heightmap at this position
				const sampleX = Math.floor(j * step);
				const sampleZ = Math.floor(i * step);
				const dataIndex = sampleZ * size + sampleX;

				// Get height value
				const height = data.heightmap[dataIndex] ?? baseY;
				const y = (height - baseY) * TerrainViewer3D.HEIGHT_SCALE;

				// Update vertex position
				position.setY(vertexIndex, y);

				// Get color (RGBA packed as uint32)
				const packedColor = data.colors[dataIndex] ?? 0x808080FF;
				const r = ((packedColor >> 24) & 0xFF) / 255;
				const g = ((packedColor >> 16) & 0xFF) / 255;
				const b = ((packedColor >> 8) & 0xFF) / 255;

				colors.push(r, g, b);
			}
		}

		// Add vertex colors
		geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
		geometry.computeVertexNormals();

		// Create material with vertex colors
		const material = new THREE.MeshLambertMaterial({
			vertexColors: true,
			side: THREE.DoubleSide,
		});

		// Create mesh and position it
		const mesh = new THREE.Mesh(geometry, material);

		// Position based on region coordinates relative to spawn
		const spawn = this._livemap.settings.spawn;
		const worldX = data.x * size - spawn.x;
		const worldZ = data.z * size - spawn.z;

		mesh.position.set(
			worldX * TerrainViewer3D.XZ_SCALE + (size * TerrainViewer3D.XZ_SCALE) / 2,
			0,
			worldZ * TerrainViewer3D.XZ_SCALE + (size * TerrainViewer3D.XZ_SCALE) / 2,
		);

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

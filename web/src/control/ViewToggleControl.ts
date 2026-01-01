import * as L from 'leaflet';

import { ControlBox } from './ControlBox';
import { TerrainViewer3D } from '../lib/TerrainViewer3D';

import type { LiveMap } from '../LiveMap';

/**
 * Control button to toggle between 2D and 3D view
 */
export class ViewToggleControl extends ControlBox {
	private readonly _dom: HTMLButtonElement;
	private readonly _viewer3D: TerrainViewer3D;
	private _is3D: boolean = false;

	constructor(livemap: LiveMap) {
		super(livemap, 'topleft');

		this._viewer3D = new TerrainViewer3D(livemap);

		// Create toggle button
		this._dom = L.DomUtil.create('button', 'leaflet-control-layers view-toggle');
		this._dom.type = 'button';
		this._dom.title = 'Toggle 3D View';
		this._dom.innerHTML = '3D';
		this._dom.style.cssText = `
			width: 34px;
			height: 34px;
			background: white;
			border: 2px solid rgba(0,0,0,0.2);
			border-radius: 4px;
			cursor: pointer;
			font-weight: bold;
			font-size: 12px;
			display: flex;
			align-items: center;
			justify-content: center;
		`;

		L.DomEvent.disableClickPropagation(this._dom);

		this._dom.addEventListener('click', (e: MouseEvent): void => {
			e.preventDefault();
			this.toggle();
		});

		// Add to map
		this.addTo(livemap);
	}

	onAdd(): HTMLButtonElement {
		return this._dom;
	}

	onRemove(): void {
		this._viewer3D.dispose();
	}

	/**
	 * Toggle between 2D and 3D view
	 */
	public toggle(): void {
		this._is3D = !this._is3D;
		this._viewer3D.toggle();
		this.updateButton();
	}

	/**
	 * Switch to 2D view
	 */
	public show2D(): void {
		if (this._is3D) {
			this._is3D = false;
			this._viewer3D.hide();
			this.updateButton();
		}
	}

	/**
	 * Switch to 3D view
	 */
	public show3D(): void {
		if (!this._is3D) {
			this._is3D = true;
			this._viewer3D.show();
			this.updateButton();
		}
	}

	private updateButton(): void {
		this._dom.innerHTML = this._is3D ? '2D' : '3D';
		this._dom.title = this._is3D ? 'Switch to 2D Map' : 'Switch to 3D View';
	}

	public get is3D(): boolean {
		return this._is3D;
	}

	public get viewer3D(): TerrainViewer3D {
		return this._viewer3D;
	}
}

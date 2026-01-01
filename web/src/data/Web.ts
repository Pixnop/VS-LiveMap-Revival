export class Web {
	private readonly _tiletype: string;
	private readonly _enable3d: boolean;

	constructor(web?: any) {
		this._tiletype = web?.tiletype ?? 'webp';
		this._enable3d = web?.enable3d ?? false;
	}

	get tiletype(): string {
		return this._tiletype;
	}

	get enable3d(): boolean {
		return this._enable3d;
	}
}

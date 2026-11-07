export class DoubleTap {
    private element: HTMLElement;
    private activate: () => void;
    private down: {
        x: number;
        y: number;
        at: number;
        id: number;
    } | null = null;
    private previous: {
        x: number;
        y: number;
        at: number;
        type: string;
    } | null = null;

    constructor(element: HTMLElement, activate: () => void) {
        this.element = element;
        this.activate = activate;
        element.addEventListener('pointerdown', this.onDown);
        element.addEventListener('pointerup', this.onUp);
        element.addEventListener('pointercancel', this.cancel);
        element.addEventListener('keydown', this.onKey);
    }

    private onDown = (event: PointerEvent): void => {
        if (!event.isPrimary || event.button !== 0 || (event.target as Element).closest('button,a,input,select,textarea,dialog')) {
            this.cancel();

            return;
        }

        this.down = {
            x: event.clientX,
            y: event.clientY,
            at: event.timeStamp,
            id: event.pointerId
        };
    };
    private onUp = (event: PointerEvent): void => {
        const down = this.down;
        this.down = null;

        if (
            !down ||
            down.id !== event.pointerId ||
            event.timeStamp - down.at > 300 ||
            Math.hypot(event.clientX - down.x, event.clientY - down.y) > 16
        ) {
            this.previous = null;

            return;
        }

        const previous = this.previous;

        if (
            previous &&
            previous.type === event.pointerType &&
            event.timeStamp - previous.at <= 350 &&
            Math.hypot(event.clientX - previous.x, event.clientY - previous.y) <= 30
        ) {
            this.previous = null;
            this.activate();
        } else {
            this.previous = {
                x: event.clientX,
                y: event.clientY,
                at: event.timeStamp,
                type: event.pointerType
            };
        }
    };
    private cancel = (): void => {
        this.down = this.previous = null;
    };
    private onKey = (event: KeyboardEvent): void => {
        if (event.target === this.element && event.key === 'Enter') {
            event.preventDefault();
            this.activate();
        }
    };

    destroy(): void {
        this.element.removeEventListener('pointerdown', this.onDown);
        this.element.removeEventListener('pointerup', this.onUp);
        this.element.removeEventListener('pointercancel', this.cancel);
        this.element.removeEventListener('keydown', this.onKey);
    }
}
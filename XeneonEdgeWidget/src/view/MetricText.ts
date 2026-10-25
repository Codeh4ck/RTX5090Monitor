export class MetricText {
    private node: HTMLElement;
    private previous = '';
    private structure: string | null = null;
    private textNodes: Text[] = [];

    constructor(node: HTMLElement) {
        this.node = node;
    }

    set(value: string): void {
        if (value === this.previous) {
            return;
        }

        this.previous = value;

        const readings = [...value.matchAll(/([+-]?\d[\d.,\s\u00a0\u202f]*)\s*(°C|°F|RPM|MHz|GB|MB|W\b|V\b|A\b|%)/g)];
        const structure = JSON.stringify(readings.map((reading) => reading[2]));
        const values: string[] = [];
        let offset = 0;

        for (const reading of readings) {
            values.push(value.slice(offset, reading.index), reading[1]!.trim(), reading[2]!);
            offset = reading.index! + reading[0].length;
        }

        values.push(value.slice(offset));

        if (structure === this.structure) {
            values.forEach((text, index) => {
                if (this.textNodes[index]!.data !== text) {
                    this.textNodes[index]!.data = text;
                }
            });

            return;
        }

        this.structure = structure;
        this.textNodes = [];

        const fragments: HTMLElement[] = [];

        for (let index = 0; index < readings.length; index++) {
            fragments.push(this.part('metric-separator', values[index * 3]!));

            const pair = document.createElement('span');
            pair.className = 'metric-part';
            pair.append(
                this.part('number', values[index * 3 + 1]!),
                document.createTextNode(readings[index]![2] === '%' ? '' : ' '),
                this.part('unit', values[index * 3 + 2]!)
            );
            fragments.push(pair);
        }

        fragments.push(this.part('metric-separator', values.at(-1)!));
        this.node.replaceChildren(...fragments);
    }

    private part(className: string, value: string): HTMLElement {
        const element = document.createElement('span');
        element.className = className;

        const text = document.createTextNode(value);
        this.textNodes.push(text);
        element.append(text);

        return element;
    }
}
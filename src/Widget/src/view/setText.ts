export function setText(node: HTMLElement, text: string): void {
    if (node.textContent !== text) {
        node.textContent = text;
    }
}

export function setVariable(node: HTMLElement, name: string, value: string): void {
    if (node.style.getPropertyValue(name) !== value) {
        node.style.setProperty(name, value);
    }
}
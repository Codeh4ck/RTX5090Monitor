import { ReadOnlyApi } from '../transport/ReadOnlyApi.ts';
import type { ConfigurationSnapshot } from '../contracts/ConfigurationSnapshot.ts';
import type { WarningNotice } from '../warnings/WarningNotice.ts';
import { configurationRows } from '../configuration/configurationRows.ts';
import type { ConfigurationRow } from '../configuration/ConfigurationRow.ts';

export class FlyoutView {
    private root: HTMLElement;
    private changed: () => void;
    private dialog: HTMLDialogElement | null = null;
    private body: HTMLElement | null = null;
    private controller: AbortController | null = null;
    private signature = '';
    mode: 'configuration' | 'warnings' | 'preview' | null = null;

    constructor(root: HTMLElement, changed: () => void) {
        this.root = root;
        this.changed = changed;
    }

    get open(): boolean {
        return this.dialog !== null;
    }

    private show(mode: NonNullable<FlyoutView['mode']>, title: string): HTMLElement {
        this.close();
        this.mode = mode;

        const dialog = document.createElement('dialog');
        dialog.className = 'widget-flyout';
        dialog.dataset.mode = mode;
        dialog.setAttribute('aria-labelledby', 'flyout-title');
        dialog.innerHTML =
            '<header class="flyout-header"><h2 id="flyout-title"></h2><button type="button" class="flyout-close" aria-label="Close flyout">✕</button></header><div class="flyout-body" tabindex="0"></div>';
        dialog.querySelector('h2')!.textContent = title;
        dialog.querySelector('button')!.addEventListener('click', () => this.close());
        dialog.addEventListener('cancel', (event) => {
            event.preventDefault();
            this.close();
        });

        this.root.append(dialog);
        this.dialog = dialog;
        this.body = dialog.querySelector('.flyout-body')!;
        this.root.querySelector<HTMLElement>('.main-content')!.hidden = true;
        dialog.showModal();
        this.changed();

        return this.body;
    }

    async configuration(): Promise<void> {
        const body = this.show('configuration', 'Backend configuration');
        const controller = new AbortController();
        this.controller = controller;

        const loading = document.createElement('p');
        loading.className = 'flyout-message';
        loading.textContent = 'Loading current service settings…';
        body.append(loading);

        try {
            const api = new ReadOnlyApi(59273);
            const snapshot = (await api.get('/configuration', controller.signal)) as ConfigurationSnapshot;

            if (controller.signal.aborted) {
                return;
            }

            if (
                !Number.isSafeInteger(snapshot?.revision) ||
                !snapshot.configuration?.plugins ||
                typeof snapshot.configuration.plugins !== 'object'
            ) {
                throw new Error('Unsupported service configuration response.');
            }

            loading.remove();
            this.section(body, `Sensor stream - revision ${snapshot.revision}`, configurationRows(snapshot.configuration.monitoring ?? {}));

            for (const [id, plugin] of Object.entries(snapshot.configuration.plugins)) {
                const name =
                    (
                        {
                            'asus-astral': 'ROG Astral',
                            'wireview-pro2': 'WireView Pro 2',
                            'msi-psu': 'MSI MPG PSU'
                        } as Record<string, string>
                    )[id] ?? id;
                this.section(body, name, configurationRows(plugin as unknown as Record<string, unknown>));

                if (id !== 'wireview-pro2') {
                    continue;
                }

                try {
                    const device = await api.get('/plugins/wireview-pro2/device/config', controller.signal);

                    if (controller.signal.aborted) {
                        return;
                    }

                    if (!device || typeof device !== 'object' || Array.isArray(device)) {
                        throw new Error('Invalid cached device configuration.');
                    }

                    this.section(body, 'WireView device settings', configurationRows(device as Record<string, unknown>));
                } catch {
                    if (controller.signal.aborted) {
                        return;
                    }

                    this.section(body, 'WireView device settings', [{ label: 'Device configuration', value: 'Unavailable' }]);
                }
            }
        } catch (error) {
            if (controller.signal.aborted) {
                return;
            }

            loading.textContent = error instanceof Error ? error.message : 'Could not read service configuration.';
            loading.classList.add('flyout-error');

            const retry = document.createElement('button');
            retry.type = 'button';
            retry.className = 'flyout-retry';
            retry.textContent = 'Retry';
            retry.addEventListener('click', () => void this.configuration());
            body.append(retry);
        }

        if (!controller.signal.aborted) {
            this.notice(body);
        }
    }

    warnings(warnings: WarningNotice[], preview = false): void {
        const signature = JSON.stringify([warnings, preview]);

        if (this.signature === signature && this.open) {
            return;
        }

        const mode = preview ? 'preview' : 'warnings';
        const body = this.mode === mode && this.body ? this.body : this.show(mode, preview ? 'Warning preview' : 'Monitoring warnings');
        const scroll = body.scrollTop;
        body.replaceChildren();

        if (preview) {
            const label = document.createElement('p');
            label.className = 'preview-notice';
            label.textContent = 'SIMULATED - telemetry and backend settings are unchanged.';
            body.append(label);
        }

        for (const warning of warnings) {
            const card = document.createElement('section');
            card.className = 'warning-card';
            card.innerHTML = '<span class="icon" aria-hidden="true"></span><div><h3></h3><p></p></div>';
            (card.querySelector('.icon')! as HTMLElement).dataset.icon = warning.icon;
            card.querySelector('h3')!.textContent = (preview && !warning.id.startsWith('preview/') ? 'LIVE - ' : '') + warning.title;
            card.querySelector('p')!.textContent = warning.detail;
            body.append(card);
        }

        this.notice(body);
        body.scrollTop = scroll;
        this.signature = signature;
    }

    private section(body: HTMLElement, title: string, rows: ConfigurationRow[]): void {
        const section = document.createElement('section');
        section.className = 'configuration-section';

        const heading = document.createElement('h3');
        heading.textContent = title;
        section.append(heading);

        for (const row of rows) {
            const element = document.createElement('div');
            element.className = 'configuration-row';

            const label = document.createElement('span');
            label.textContent = row.label;

            const value = document.createElement('b');
            value.textContent = row.value;
            element.append(label, value);
            section.append(element);
        }

        body.append(section);
    }

    private notice(body: HTMLElement): void {
        const notice = document.createElement('p');
        notice.className = 'configuration-notice';
        notice.textContent = 'Use the RTX Monitor desktop app to change backend service configuration.';
        body.append(notice);
    }

    close(): void {
        this.controller?.abort();
        this.controller = null;
        this.dialog?.close();
        this.dialog?.remove();
        this.dialog = this.body = null;
        this.mode = null;
        this.signature = '';
        this.root.querySelector<HTMLElement>('.main-content')!.hidden = false;
        this.changed();
    }
}
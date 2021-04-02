import { EventEmitter } from 'events';
import { Client as DeviceClient, DeviceMethodRequest, DeviceMethodResponse, Message, Twin } from 'azure-iot-device'
import { Mqtt as DeviceTransport } from 'azure-iot-device-mqtt'
import { ProvisioningDeviceClient } from 'azure-iot-provisioning-device'
import { Mqtt as ProvisioningTransport } from 'azure-iot-provisioning-device-mqtt'
import { RegistrationResult } from 'azure-iot-provisioning-device';
import { DeviceConfig } from './types';
import { SymmetricKeySecurityClient } from 'azure-iot-security-symmetric-key';
import * as fs from 'fs';

const REGISTER_RETRY_INTERVAL = 5000;
const TELEMETRY_INTERVAL = 1000;

export enum DeviceState {
    Disconnected = 0,
    Registering = 1,
    Registered = 2,
    RegistrationError = 3,
    Connecting = 4,
    Connected = 5
}

export const enum DeviceEvents {
    Registering = 'registering',
    Registered = 'registered',
    RegistrationError = 'registration_err',
    Connecting = 'connecting',
    Connected = 'connected',
    ConnectionError = 'connection_err',
    TwinDesiredUpdate = 'twin_desired_update',
    TwinReportedUpdate = 'twin_reported_update',
    TwinReportedUpdateError = 'twin_reported_update_err',
    CommandReceived = 'command_received',
    CommandResponse = 'command_response',
    CommandCompleted = 'command_completed',
    TelemetrySent = 'telemetry_sent',
    TelemetryError = 'telemetry_err'
}

export default class SmartMeterDevice extends EventEmitter {
    
    private _deviceId: string;
    private _deviceKey: string;
    private _idScope: string;
    private _gatewayName: string;
    private _deviceClient: DeviceClient | undefined;
    private _currentTwin: Twin | undefined;
    private _currentState: DeviceState = DeviceState.Disconnected;
    private _registration: RegistrationResult | undefined;
    private _telemetryTimeout: NodeJS.Timeout | undefined; 
    private _certPath: string;

    constructor(config: DeviceConfig) {
        super();

        this._deviceId = config.deviceId;
        this._deviceKey = config.deviceKey;
        this._idScope = config.idScope;
        this._gatewayName = config.gatewayName;
        this._certPath = "./certs/azure-iot-test-only.root.ca.cert.pem";
    }

    get id(): string {
        return this._deviceId;
    }

    get gateway(): string {
        return this._gatewayName;
    }

    get idScope(): string {
        return this._idScope;
    }

    get assignedHub(): string | undefined {
        return this._registration?.assignedHub;
    }

    get state(): DeviceState {
        return this._currentState;
    }

    start() {
        this._register();
    }

    private _register() {
        this._currentState = DeviceState.Registering;
        this.emit(DeviceEvents.Registering);

        const transport = new ProvisioningTransport();
        const client = ProvisioningDeviceClient.create(
            'global.azure-devices-provisioning.net',
            this._idScope,
            transport,
            new SymmetricKeySecurityClient(this._deviceId, this._deviceKey)
        );

        client.register((err, registration) => {
            if (err) {
                this.emit(DeviceEvents.RegistrationError, err);
                this._currentState = DeviceState.RegistrationError;

                // retry in 5 seconds.
                setTimeout(this._register.bind(this), REGISTER_RETRY_INTERVAL);
                return;
            }

            this._currentState = DeviceState.Registered;
            this._registration = registration;
            this.emit(DeviceEvents.Registered);
            this._connect();
        });
    }

    private _connect() {
        this._currentState = DeviceState.Connecting;
        this.emit(DeviceEvents.Connecting);

        const connectionString = `HostName=${this._registration!.assignedHub};DeviceId=${this._registration!.deviceId};SharedAccessKey=${this._deviceKey};GatewayHostName=${this._gatewayName}`;

        const deviceClient = DeviceClient.fromConnectionString(connectionString, DeviceTransport);
        var options = {
            ca : fs.readFileSync(this._certPath, 'utf-8'),
        };

        deviceClient.setOptions(options, async (err) => {
            if (err) {
                console.log('SetOptions Error: ' + err);
            } else {
                deviceClient.open(async (err) => {
                    if (err) {
                        this.emit(DeviceEvents.ConnectionError, err);
                        await this._disconnect();
                        // retry registration in 5 seconds.
                        setTimeout(this._register.bind(this), REGISTER_RETRY_INTERVAL);
                    } else {
                        this._deviceClient = deviceClient;
                        this._currentTwin = await this._deviceClient.getTwin();
                        this._currentState = DeviceState.Connected;
                        this.emit(DeviceEvents.Connected);
                        this._initializeHandlers();
                        this._scheduleTelemetry();
                    }
                });
            }
        });
    }

    private async _disconnect() {
        if (this._telemetryTimeout) {
            clearTimeout(this._telemetryTimeout);
        }

        if (this._currentTwin) {
            this._currentTwin.removeAllListeners();
        }
        if (this._deviceClient) {
            this._deviceClient.removeAllListeners();
            await this._deviceClient.close();
        }

        this._registration = undefined;
        this._deviceClient = undefined;
        this._currentState = DeviceState.Disconnected;
    }

    private _initializeHandlers() {
        this._deviceClient!.onDeviceMethod('reprovision', this._onReprovisionDevice.bind(this));
    }

    private async _onReprovisionDevice(request: DeviceMethodRequest, response: DeviceMethodResponse) {
        this._idScope = request.payload;
        response.send(200);

        // disconnect the client first.
        await this._disconnect();

        // re-register the device.
        this._register();
    }

    private _scheduleTelemetry() {
        this._telemetryTimeout = setTimeout(this._sendTelemetry.bind(this), TELEMETRY_INTERVAL);
    }

    private async _sendTelemetry() {
        this._telemetryTimeout = undefined;
        try {
            const message = new Message(JSON.stringify(this._createTelemetry()));
            message.contentType = 'application/json';
            await this._deviceClient?.sendEvent(message)
            this.emit(DeviceEvents.TelemetrySent);
        } catch (err) {
            this.emit(DeviceEvents.TelemetryError, err);
        } finally {
            this._scheduleTelemetry();
        }
    }

    private _createTelemetry() {
        return {
            temperature: 20 + (Math.random() * 10),
            pressure: 40 + (Math.random() * 20),
            humidity: 60 + (Math.random() * 20),
            scale: 'celsius'
        }
    }
}
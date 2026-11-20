<?php
/**
 * Plugin Name: Lyra Studio Preview Bridge
 * Description: Optional, authenticated temporary-draft preview transport for Lyra Studio and Elementor. Does not modify Lyra Core.
 * Version: 0.1.0
 * Requires at least: 6.0
 * Requires PHP: 7.4
 * Author: Lyra
 * License: GPL-2.0-or-later
 * Text Domain: lyra-studio-preview-bridge
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

final class Lyra_Studio_Preview_Bridge {
	const VERSION          = '0.1.0';
	const REST_NAMESPACE   = 'lyra-studio/v1';
	const META_SESSION     = '_lyra_studio_preview_session';
	const META_OWNER       = '_lyra_studio_preview_owner';
	const META_EXPIRES     = '_lyra_studio_preview_expires_at';
	const CRON_HOOK        = 'lyra_studio_preview_bridge_cleanup';
	const MAX_JSON_BYTES   = 2097152;
	const DEFAULT_TTL      = 1800;
	const MAX_TTL          = 7200;
	const MAX_SESSIONS     = 5;

	public static function boot() {
		add_action( 'rest_api_init', array( __CLASS__, 'register_routes' ) );
		add_action( self::CRON_HOOK, array( __CLASS__, 'cleanup_expired_sessions' ) );
	}

	public static function activate() {
		if ( ! wp_next_scheduled( self::CRON_HOOK ) ) {
			wp_schedule_single_event( time() + 300, self::CRON_HOOK );
		}
	}

	public static function deactivate() {
		wp_clear_scheduled_hook( self::CRON_HOOK );
	}

	public static function register_routes() {
		register_rest_route(
			self::REST_NAMESPACE,
			'/capabilities',
			array(
				'methods'             => WP_REST_Server::READABLE,
				'callback'            => array( __CLASS__, 'get_capabilities' ),
				'permission_callback' => array( __CLASS__, 'check_permission' ),
			)
		);

		register_rest_route(
			self::REST_NAMESPACE,
			'/previews',
			array(
				'methods'             => WP_REST_Server::CREATABLE,
				'callback'            => array( __CLASS__, 'create_preview' ),
				'permission_callback' => array( __CLASS__, 'check_permission' ),
			)
		);

		register_rest_route(
			self::REST_NAMESPACE,
			'/previews/(?P<session_id>[0-9a-fA-F-]{36})',
			array(
				array(
					'methods'             => WP_REST_Server::EDITABLE,
					'callback'            => array( __CLASS__, 'update_preview' ),
					'permission_callback' => array( __CLASS__, 'check_permission' ),
				),
				array(
					'methods'             => WP_REST_Server::DELETABLE,
					'callback'            => array( __CLASS__, 'delete_preview' ),
					'permission_callback' => array( __CLASS__, 'check_permission' ),
				),
			)
		);
	}

	public static function check_permission() {
		if ( ! is_user_logged_in() || ! current_user_can( 'edit_pages' ) ) {
			return new WP_Error(
				'lyra_studio_forbidden',
				__( 'An authenticated WordPress user with page-editing permission is required.', 'lyra-studio-preview-bridge' ),
				array( 'status' => rest_authorization_required_code() )
			);
		}

		return true;
	}

	public static function get_capabilities() {
		$elementor_ready = self::elementor_is_ready();
		$widget_names    = array();

		if ( $elementor_ready && isset( \Elementor\Plugin::$instance->widgets_manager ) ) {
			$widgets = \Elementor\Plugin::$instance->widgets_manager->get_widget_types();
			foreach ( $widgets as $widget ) {
				if ( is_object( $widget ) && method_exists( $widget, 'get_name' ) ) {
					$widget_names[] = sanitize_key( $widget->get_name() );
				}
			}
		}

		sort( $widget_names, SORT_STRING );

		return rest_ensure_response(
			array(
				'bridge_version'  => self::VERSION,
				'wordpress'       => get_bloginfo( 'version' ),
				'elementor_ready' => $elementor_ready,
				'elementor'       => defined( 'ELEMENTOR_VERSION' ) ? ELEMENTOR_VERSION : null,
				'can_edit_pages'  => current_user_can( 'edit_pages' ),
				'widget_types'    => array_values( array_unique( $widget_names ) ),
				'preview_mode'    => 'temporary-draft',
				'maximum_ttl'     => self::MAX_TTL,
			)
		);
	}

	public static function create_preview( WP_REST_Request $request ) {
		if ( ! self::elementor_is_ready() ) {
			return new WP_Error(
				'lyra_studio_elementor_unavailable',
				__( 'Elementor must be active before Lyra Studio previews can be created.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 409 )
			);
		}

		$body    = $request->get_json_params();
		$payload = self::validate_payload( $body );

		if ( is_wp_error( $payload ) ) {
			return $payload;
		}

		$user_id = get_current_user_id();
		if ( self::count_active_sessions( $user_id ) >= self::MAX_SESSIONS ) {
			return new WP_Error(
				'lyra_studio_session_limit',
				__( 'The maximum number of active Lyra Studio preview drafts has been reached. Close an existing preview and try again.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 429 )
			);
		}

		$session_id = wp_generate_uuid4();
		$expires_at = time() + $payload['ttl'];
		$title      = sprintf(
			/* translators: 1: optional user title, 2: short session identifier. */
			__( 'Lyra Studio Preview — %1$s — %2$s', 'lyra-studio-preview-bridge' ),
			$payload['title'],
			substr( str_replace( '-', '', $session_id ), 0, 8 )
		);

		$post_data = array(
			'post_type'      => 'page',
			'post_status'    => 'draft',
			'post_author'    => $user_id,
			'post_title'     => $title,
			'post_content'   => '',
			'comment_status' => 'closed',
			'ping_status'    => 'closed',
		);

		$meta_data = array(
			self::META_SESSION => $session_id,
			self::META_OWNER   => $user_id,
			self::META_EXPIRES => $expires_at,
		);

		try {
			$document = \Elementor\Plugin::$instance->documents->create( 'post', $post_data, $meta_data );

			if ( is_wp_error( $document ) || ! is_object( $document ) || ! method_exists( $document, 'save' ) ) {
				return new WP_Error(
					'lyra_studio_document_create_failed',
					__( 'Elementor could not create the temporary preview document.', 'lyra-studio-preview-bridge' ),
					array( 'status' => 500 )
				);
			}

			$document->save(
				array(
					'elements' => $payload['elements'],
					'settings' => $payload['settings'],
				)
			);

			$post = $document->get_post();
			if ( ! $post || 'page' !== $post->post_type || 'draft' !== $post->post_status ) {
				if ( $post ) {
					self::delete_exact_draft( $post->ID, $session_id, $user_id );
				}
				return new WP_Error(
					'lyra_studio_draft_not_created',
					__( 'The preview document was not retained as a draft. No published page was created.', 'lyra-studio-preview-bridge' ),
					array( 'status' => 500 )
				);
			}

			$preview_url = method_exists( $document, 'get_wp_preview_url' )
				? $document->get_wp_preview_url()
				: get_preview_post_link( $post );

			if ( empty( $preview_url ) ) {
				self::delete_exact_draft( $post->ID, $session_id, $user_id );
				return new WP_Error(
					'lyra_studio_preview_url_failed',
					__( 'WordPress did not provide a preview URL for the temporary draft.', 'lyra-studio-preview-bridge' ),
					array( 'status' => 500 )
				);
			}

			return rest_ensure_response(
				array(
					'session_id'    => $session_id,
					'post_id'       => (int) $post->ID,
					'status'        => $post->post_status,
					'preview_url'   => esc_url_raw( $preview_url ),
					'expires_at'    => gmdate( 'c', $expires_at ),
					'elementor'     => defined( 'ELEMENTOR_VERSION' ) ? ELEMENTOR_VERSION : null,
				)
			);
		} catch ( Throwable $error ) {
			if ( isset( $document ) && is_object( $document ) && method_exists( $document, 'get_post' ) ) {
				$created_post = $document->get_post();
				if ( $created_post ) {
					self::delete_exact_draft( $created_post->ID, $session_id, $user_id );
				}
			}

			return new WP_Error(
				'lyra_studio_preview_create_error',
				__( 'The temporary Elementor preview could not be created.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 500 )
			);
		}
	}

	public static function update_preview( WP_REST_Request $request ) {
		$session_id = self::sanitize_session_id( $request->get_param( 'session_id' ) );
		$post       = self::find_session_post( $session_id );

		if ( is_wp_error( $post ) ) {
			return $post;
		}

		$access = self::validate_owned_draft( $post, $session_id );
		if ( is_wp_error( $access ) ) {
			return $access;
		}

		$body    = $request->get_json_params();
		$payload = self::validate_payload( $body, false );

		if ( is_wp_error( $payload ) ) {
			return $payload;
		}

		try {
			$document = \Elementor\Plugin::$instance->documents->get( $post->ID );
			if ( ! $document || ! $document->is_editable_by_current_user() ) {
				return new WP_Error(
					'lyra_studio_document_not_editable',
					__( 'The temporary Elementor document is no longer editable by this user.', 'lyra-studio-preview-bridge' ),
					array( 'status' => 403 )
				);
			}

			$document->save(
				array(
					'elements' => $payload['elements'],
					'settings' => $payload['settings'],
				)
			);

			$updated_post = get_post( $post->ID );
			if ( ! $updated_post || 'draft' !== $updated_post->post_status ) {
				return new WP_Error(
					'lyra_studio_draft_status_changed',
					__( 'The preview document is no longer a draft. Lyra Studio did not change or delete it.', 'lyra-studio-preview-bridge' ),
					array( 'status' => 409 )
				);
			}

			$expires_at = time() + $payload['ttl'];
			update_post_meta( $post->ID, self::META_EXPIRES, $expires_at );

			$preview_url = method_exists( $document, 'get_wp_preview_url' )
				? $document->get_wp_preview_url()
				: get_preview_post_link( $updated_post );

			return rest_ensure_response(
				array(
					'session_id'  => $session_id,
					'post_id'     => (int) $post->ID,
					'status'      => $updated_post->post_status,
					'preview_url' => esc_url_raw( $preview_url ),
					'expires_at'  => gmdate( 'c', $expires_at ),
				)
			);
		} catch ( Throwable $error ) {
			return new WP_Error(
				'lyra_studio_preview_update_error',
				__( 'The temporary Elementor preview could not be updated.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 500 )
			);
		}
	}

	public static function delete_preview( WP_REST_Request $request ) {
		$session_id = self::sanitize_session_id( $request->get_param( 'session_id' ) );
		$post       = self::find_session_post( $session_id );

		if ( is_wp_error( $post ) ) {
			return $post;
		}

		$access = self::validate_owned_draft( $post, $session_id, true );
		if ( is_wp_error( $access ) ) {
			return $access;
		}

		if ( 'draft' !== $post->post_status ) {
			return new WP_Error(
				'lyra_studio_refuse_delete',
				__( 'The document is no longer a draft. It has been left untouched.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 409 )
			);
		}

		$deleted = self::delete_exact_draft( $post->ID, $session_id, get_current_user_id() );
		if ( ! $deleted ) {
			return new WP_Error(
				'lyra_studio_delete_failed',
				__( 'WordPress could not delete the exact temporary preview draft.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 500 )
			);
		}

		return rest_ensure_response(
			array(
				'session_id' => $session_id,
				'deleted'    => true,
			)
		);
	}

	public static function cleanup_expired_sessions() {
		$now = time();
		$ids = get_posts(
			array(
				'post_type'      => 'page',
				'post_status'    => 'draft',
				'posts_per_page' => 100,
				'fields'         => 'ids',
				'meta_query'     => array(
					'relation' => 'AND',
					array(
						'key'     => self::META_SESSION,
						'compare' => 'EXISTS',
					),
					array(
						'key'     => self::META_EXPIRES,
						'value'   => $now,
						'compare' => '<',
						'type'    => 'NUMERIC',
					),
				),
			)
		);

		foreach ( $ids as $post_id ) {
			$post       = get_post( $post_id );
			$session_id = get_post_meta( $post_id, self::META_SESSION, true );

			$owner_id = (int) get_post_meta( $post_id, self::META_OWNER, true );
			if ( $post && 'draft' === $post->post_status && self::is_valid_session_id( $session_id ) && $owner_id > 0 ) {
				self::delete_exact_draft( $post_id, $session_id, $owner_id );
			}
		}

		$remaining = get_posts(
			array(
				'post_type'      => 'page',
				'post_status'    => 'draft',
				'posts_per_page' => 1,
				'fields'         => 'ids',
				'meta_query'     => array( array( 'key' => self::META_SESSION, 'compare' => 'EXISTS' ) ),
			)
		);
		if ( ! empty( $remaining ) ) {
			self::schedule_cleanup();
		}
	}

	private static function validate_payload( $body, $include_ttl = true ) {
		if ( ! is_array( $body ) || ! isset( $body['elements'] ) || ! is_array( $body['elements'] ) ) {
			return new WP_Error(
				'lyra_studio_invalid_elements',
				__( 'The request must contain an Elementor elements array.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 400 )
			);
		}

		foreach ( $body['elements'] as $element ) {
			if ( ! is_array( $element ) ) {
				return new WP_Error(
					'lyra_studio_invalid_element',
					__( 'Each top-level Elementor element must be an object.', 'lyra-studio-preview-bridge' ),
					array( 'status' => 400 )
				);
			}
		}

		$settings = isset( $body['settings'] ) && is_array( $body['settings'] )
			? $body['settings']
			: array();
		$json = wp_json_encode( array( 'elements' => $body['elements'], 'settings' => $settings ) );
		if ( false === $json || strlen( $json ) > self::MAX_JSON_BYTES ) {
			return new WP_Error(
				'lyra_studio_payload_too_large',
				__( 'The Elementor document is invalid or exceeds the 2 MB preview limit.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 413 )
			);
		}

		$title = isset( $body['title'] )
			? sanitize_text_field( $body['title'] )
			: __( 'Widget preview', 'lyra-studio-preview-bridge' );

		$ttl = self::DEFAULT_TTL;
		if ( $include_ttl && isset( $body['ttl_seconds'] ) ) {
			$ttl = absint( $body['ttl_seconds'] );
		}
		$ttl = min( max( $ttl, 300 ), self::MAX_TTL );

		return array(
			'elements' => $body['elements'],
			'settings' => $settings,
			'title'    => $title ? $title : __( 'Widget preview', 'lyra-studio-preview-bridge' ),
			'ttl'      => $ttl,
		);
	}

	private static function validate_owned_draft( $post, $session_id, $allow_expired = false ) {
		if ( ! $post || 'page' !== $post->post_type ) {
			return new WP_Error(
				'lyra_studio_preview_not_found',
				__( 'The preview session was not found.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 404 )
			);
		}

		$stored_session = get_post_meta( $post->ID, self::META_SESSION, true );
		$owner_id       = (int) get_post_meta( $post->ID, self::META_OWNER, true );

		if ( ! is_string( $stored_session ) || ! hash_equals( $stored_session, $session_id ) ||
			$owner_id !== get_current_user_id() || ! current_user_can( 'edit_post', $post->ID ) ) {
			return new WP_Error(
				'lyra_studio_preview_forbidden',
				__( 'This preview session does not belong to the current WordPress user.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 403 )
			);
		}

		$expires_at = (int) get_post_meta( $post->ID, self::META_EXPIRES, true );
		if ( ! $allow_expired && ( ! $expires_at || $expires_at < time() ) ) {
			return new WP_Error(
				'lyra_studio_preview_expired',
				__( 'The preview session has expired. Create a new preview.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 410 )
			);
		}

		return true;
	}

	private static function find_session_post( $session_id ) {
		if ( ! self::is_valid_session_id( $session_id ) ) {
			return new WP_Error(
				'lyra_studio_invalid_session',
				__( 'The preview session identifier is invalid.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 400 )
			);
		}

		$posts = get_posts(
			array(
				'post_type'      => 'page',
				'post_status'    => array_values( get_post_stati() ),
				'posts_per_page' => 1,
				'fields'         => 'all',
				'meta_query'     => array(
					array(
						'key'   => self::META_SESSION,
						'value' => $session_id,
					),
				),
			)
		);

		if ( empty( $posts ) ) {
			return new WP_Error(
				'lyra_studio_preview_not_found',
				__( 'The preview session was not found.', 'lyra-studio-preview-bridge' ),
				array( 'status' => 404 )
			);
		}

		return $posts[0];
	}

	private static function count_active_sessions( $user_id ) {
		$posts = get_posts(
			array(
				'post_type'      => 'page',
				'post_status'    => 'draft',
				'author'         => absint( $user_id ),
				'posts_per_page' => self::MAX_SESSIONS + 1,
				'fields'         => 'ids',
				'meta_query'     => array(
					'relation' => 'AND',
					array(
						'key'     => self::META_SESSION,
						'compare' => 'EXISTS',
					),
					array(
						'key'     => self::META_EXPIRES,
						'value'   => time(),
						'compare' => '>',
						'type'    => 'NUMERIC',
					),
				),
			)
		);

		return count( $posts );
	}

	private static function delete_exact_draft( $post_id, $session_id, $owner_id ) {
		$post = get_post( $post_id );
		if ( ! $post || 'page' !== $post->post_type || 'draft' !== $post->post_status ) {
			return false;
		}

		$stored_session = get_post_meta( $post_id, self::META_SESSION, true );
		$stored_owner = (int) get_post_meta( $post_id, self::META_OWNER, true );
		if ( ! is_string( $stored_session ) || ! hash_equals( $stored_session, $session_id ) ||
			$stored_owner !== (int) $owner_id ) {
			return false;
		}

		return (bool) wp_delete_post( $post_id, true );
	}

	private static function schedule_cleanup() {
		if ( ! wp_next_scheduled( self::CRON_HOOK ) ) {
			wp_schedule_single_event( time() + 300, self::CRON_HOOK );
		}
	}

	private static function sanitize_session_id( $session_id ) {
		return is_string( $session_id ) ? strtolower( trim( $session_id ) ) : '';
	}

	private static function is_valid_session_id( $session_id ) {
		return is_string( $session_id ) &&
			(bool) preg_match( '/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i', $session_id );
	}

	private static function elementor_is_ready() {
		return did_action( 'elementor/loaded' ) &&
			class_exists( '\\Elementor\\Plugin' ) &&
			isset( \Elementor\Plugin::$instance->documents );
	}
}

add_action( 'plugins_loaded', array( 'Lyra_Studio_Preview_Bridge', 'boot' ), 20 );
register_activation_hook( __FILE__, array( 'Lyra_Studio_Preview_Bridge', 'activate' ) );
register_deactivation_hook( __FILE__, array( 'Lyra_Studio_Preview_Bridge', 'deactivate' ) );
